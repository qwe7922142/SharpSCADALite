using ClientDriver;
using DataService;
using FileDriver;
using ModbusDriver;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.ServiceModel;
using System.Text;
using System.Threading;
using System.Timers;
using System.Xml;

namespace BatchCoreService
{
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single, Namespace = "http://BatchCoreService")]
    public class DAService : IDataServer
    {
        const int PORT = 6543;
        const char SPLITCHAR = '.';

        //可配置参数，从XML文件读取
        int CYCLE = 60000;
        int SENDTIMEOUT = 60000;

        private System.Timers.Timer timer1 = new System.Timers.Timer();

        #region DAServer（标签数据服务器）
        public ITag this[short id]
        {
            get
            {
                int index = GetItemProperties(id);
                if (index >= 0)
                {
                    return this[_list[index].Name];
                }
                return null;
            }
        }

        public ITag this[string name]
        {
            get
            {
                if (string.IsNullOrEmpty(name)) return null;
                ITag dataItem;
                _mapping.TryGetValue(name.ToUpper(), out dataItem);
                return dataItem;
            }
        }

        List<TagMetaData> _list;
        public IList<TagMetaData> MetaDataList
        {
            get
            {
                return _list;
            }
        }

        public IList<Scaling> ScalingList
        {
            get
            {
                return _scales;
            }
        }

        object _syncRoot;
        public object SyncRoot
        {
            get
            {
                if (this._syncRoot == null)
                {
                    Interlocked.CompareExchange(ref this._syncRoot, new object(), null);
                }
                return this._syncRoot;
            }
        }

        List<DriverArgumet> _arguments = new List<DriverArgumet>();
        Dictionary<short, ArchiveTime> _archiveTimes = new Dictionary<short, ArchiveTime>();

        Socket tcpServer = null;

        Dictionary<IPAddress, Socket> _socketThreadList;
        public Dictionary<IPAddress, Socket> SocketList
        {
            get
            {
                return _socketThreadList;
            }
        }

        Dictionary<string, ITag> _mapping;

        List<Scaling> _scales;

        SortedList<short, IDriver> _drivers;
        public IEnumerable<IDriver> Drivers
        {
            get { return _drivers.Values; }
        }


        ExpressionEval reval;
        public ExpressionEval Eval
        {
            get
            {
                return reval;
            }
        }

        public DAService()
        {
            _scales = new List<Scaling>();
            _drivers = new SortedList<short, IDriver>();
            reval = new ExpressionEval(this);
            InitServerByJson();
            InitConnection();
            _socketThreadList = new Dictionary<IPAddress, Socket>();
            InitHost();
            timer1.Elapsed += timer1_Elapsed;
            timer1.Interval = CYCLE;
            timer1.Enabled = true;
            timer1.Start();
        }

        public void Dispose()
        {
            lock (this)
            {
                try
                {
                    if (timer1 != null)
                        timer1.Dispose();
                    if (_drivers != null)
                    {
                        foreach (var driver in Drivers)
                        {
                            driver.OnError -= this.reader_OnClose;
                            driver.Dispose();
                        }

                        foreach (var socket in _socketThreadList.Values)
                        {
                            socket.Dispose();
                        }
                        if (tcpServer != null && tcpServer.Connected)
                            tcpServer.Disconnect(false);

                        _mapping.Clear();
                        reval.Dispose();
                    }
                }
                catch (Exception e)
                {
                    AddErrorLog(e);
                }
            }
        }

        public void AddErrorLog(Exception e)
        {
            Console.Write(e.ToString());
        }

        private void timer1_Elapsed(object sender, ElapsedEventArgs e)
        {
            foreach (IDriver d in Drivers)
            {
                if (d.IsClosed)
                {
                    d.Connect();//t.IsAlive可加入判断；如线程异常，重新启动。
                }
            }
        }

        void InitConnection()
        {
            foreach (IDriver reader in _drivers.Values)
            {
                reader.OnError += new IOErrorEventHandler(reader_OnClose);
                if (reader.IsClosed)
                {
                    //if (reader is IFileDriver)
                    reader.Connect();
                }
                foreach (IGroup grp in reader.Groups)
                {
                    grp.DataChange += new DataChangeEventHandler(grp_DataChange);
                    //可在此加入判断，如为ClientDriver发出，则变化数据毋须广播，只需归档。
                    grp.IsActive = grp.IsActive;
                }
            }
            //此处需改进,与Condition采用相同的处理方式，可配置
        }
      
        void InitServerByJson()
        {
            _arguments = DataHelper.GetDriverArgumetByJson();
            List<DriverMetaData> drivers = DataHelper.GetDriverMetaDataByJson();
            foreach (var model in drivers)
            {
                AddDriver1(model.ID, model.Name, model.Assembly, model.ClassName);
            }
            _list = DataHelper.GetTagMetaDataByJson();
            List<GroupMeta> list = DataHelper.GetGroupMetaByJson();
            _mapping = new Dictionary<string, ITag>(_list.Count);
            foreach (var grpm in list)
            {
                IDriver dv;
                _drivers.TryGetValue(grpm.DriverID, out dv);
                IGroup grp = dv.AddGroup(grpm.Name, grpm.ID, grpm.UpdateRate, grpm.DeadBand, grpm.Active);
                if (grp != null)
                    grp.AddItems(_list);
            }
            reval.Clear();
            _scales.Sort();
        }
        void InitHost()
        {
            /*对关闭状态的判断，最好用心跳检测；冗余切换，可广播冗余命令，包含新主机名、数据库连接、IP地址等。
             * 服务启动时，向整个局域网UDP广播加密的主机名、连接字符串等信息
             */
            //socketThreadList = new Dictionary<IPAddress, Socket>();
            tcpServer = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            IPEndPoint LocalPort = new IPEndPoint(IPAddress.Any, PORT);
            tcpServer.Bind(LocalPort);
            tcpServer.Listen(100);
            ThreadPool.QueueUserWorkItem(new WaitCallback(AcceptWorkThread));
        }

        void AcceptWorkThread(object state)
        {
            while (true)
            {
                //if (tcpServer.Poll(0, SelectMode.SelectRead))
                Socket s_Accept = tcpServer.Accept();
                //IPAddress addr = (s_Accept.RemoteEndPoint as IPEndPoint).Address;
                s_Accept.SendTimeout = SENDTIMEOUT;
                IPAddress addr = (s_Accept.RemoteEndPoint as IPEndPoint).Address;
                try
                {
                    if (!_socketThreadList.ContainsKey(addr))
                        _socketThreadList.Add(addr, s_Accept);
                }
                catch (Exception err)
                {
                    AddErrorLog(err);
                }
                ThreadPool.UnsafeQueueUserWorkItem(new WaitCallback(ReceiveWorkThread), s_Accept);
            }
        }

        void ReceiveWorkThread(object obj)
        {
            Socket s_Receive = (Socket)obj;
            IPAddress addr = null;
            try
            {
                addr = (s_Receive.RemoteEndPoint as IPEndPoint).Address;
            }
            catch (Exception err)
            {
                AddErrorLog(err);
                return;
            }
            byte[] buffer = new byte[s_Receive.ReceiveBufferSize];     // 创建接收缓冲
            while (true)
            {
                try
                {
                    if (addr == null || !_socketThreadList.ContainsKey(addr)) return;
                    /*if (!s_Receive.Connected) return;
                    关于数据传输协议：命令可分为：订单指令（订单类型，增删改标记可各用一个字段，路径ID用GUID，路径状态包括暂停、继续
                    、终止、启动）；可返回客户端一个可行的路径设备链、ERP交换数据指令（包含DATASET)，冗余切换指令等）
                     */
                    int ReceiveCount = s_Receive.Receive(buffer);

                    if (buffer[0] == FCTCOMMAND.fctHead)
                    {
                        //buffer[0]是协议头，1是指令号，2是读方式（缓存还是设备），3、4是ID，5是长度，后接变量值
                        byte command = buffer[1];
                        switch (command)
                        {
                            case FCTCOMMAND.fctReadSingle:
                                {
                                    //DataSource source = buffer[2] == 0 ? DataSource.Cache : DataSource.Device;
                                    short id = BitConverter.ToInt16(buffer, 3);
                                    byte length = buffer[5];
                                    byte[] send = new byte[5 + length];
                                    for (int i = 0; i < 5; i++)
                                    {
                                        send[i] = buffer[i];
                                    }
                                    ITag tag = this[id];
                                    if (tag != null)
                                    {
                                        Storage value = buffer[2] == 0 ? tag.Value : tag.Read(DataSource.Device);
                                        byte[] bt = tag.ToByteArray(value);
                                        for (int k = 0; k < bt.Length; k++)
                                        {
                                            send[5 + k] = bt[k];
                                        }
                                    }
                                    else
                                    {
                                        //出错处理,可考虑返回一个DATATYPE.NONE类型
                                    }
                                    s_Receive.Send(send);
                                }
                                break;
                            case FCTCOMMAND.fctReadMultiple:
                                {
                                    //buffer[0]是协议头，1是指令号，2是读方式（缓存还是设备），3、4是变量数，后接变量值
                                    //DataSource source = buffer[2] == 0 ? DataSource.Cache : DataSource.Device;
                                    byte[] send = new byte[s_Receive.SendBufferSize];
                                    send[0] = FCTCOMMAND.fctHead;
                                    short count = BitConverter.ToInt16(buffer, 3);//要读取的变量数
                                    int j = 5; int l = 5;
                                    if (buffer[2] == 0)
                                    {
                                        for (int i = 0; i < count; i++)
                                        {
                                            short id = BitConverter.ToInt16(buffer, l);
                                            send[j++] = buffer[l++];
                                            send[j++] = buffer[l++];
                                            ITag tag = this[id];
                                            if (tag != null)
                                            {
                                                byte[] bt = tag.ToByteArray();
                                                var length = (byte)bt.Length;
                                                send[j++] = length;
                                                for (int k = 0; k < length; k++)
                                                {
                                                    send[j + k] = bt[k];
                                                }
                                                j += length;
                                            }
                                            else
                                            {//类型后跟长度
                                                send[j++] = 0;
                                            }
                                        }
                                    }
                                    else
                                    {
                                        Dictionary<IGroup, List<ITag>> dict = new Dictionary<IGroup, List<ITag>>();
                                        for (int i = 0; i < count; i++)
                                        {
                                            short id = BitConverter.ToInt16(buffer, l);
                                            l += 2;
                                            ITag tag = this[id];
                                            if (tag != null)
                                            {
                                                IGroup grp = tag.Parent;
                                                if (!dict.ContainsKey(grp))
                                                    dict.Add(grp, new List<ITag> { tag });
                                                else
                                                    dict[grp].Add(tag);
                                            }
                                        }
                                        foreach (var dev in dict)
                                        {
                                            var list = dev.Value;
                                            var array = dev.Key.BatchRead(DataSource.Device, true, list.ToArray());
                                            if (array == null) continue;
                                            for (int i = 0; i < list.Count; i++)
                                            {
                                                byte[] bt = list[i].ToByteArray(array[i].Value);
                                                var length = (byte)bt.Length;
                                                send[j++] = length;
                                                for (int k = 0; k < bt.Length; k++)
                                                {
                                                    send[j + k] = bt[k];
                                                }
                                                j += length;
                                            }
                                        }
                                    }
                                    s_Receive.Send(send, 0, j, SocketFlags.None);
                                }
                                break;
                            case FCTCOMMAND.fctWriteSingle:
                                {
                                    //buffer[0]是协议头，1是指令号，2是写方式（缓存还是设备），3、4是ID，5是长度
                                    short id = BitConverter.ToInt16(buffer, 3);
                                    byte rs = 0;
                                    ITag tag = this[id];
                                    if (tag != null)//此处应考虑万一写失败，是否需要更新值
                                    {
                                        if (tag.Address.VarType == DataType.STR)
                                        {
                                            StringTag strTag = tag as StringTag;
                                            if (strTag != null)
                                            {
                                                string txt = Encoding.ASCII.GetString(buffer, 6, buffer[5]).Trim((char)0);
                                                rs = (byte)tag.Write(txt);
                                                if (rs == 0)
                                                    strTag.String = txt;
                                            }
                                        }
                                        else
                                        {
                                            Storage value = Storage.Empty;
                                            switch (tag.Address.VarType)
                                            {
                                                case DataType.BOOL:
                                                    value.Boolean = BitConverter.ToBoolean(buffer, 6);
                                                    break;
                                                case DataType.BYTE:
                                                    value.Byte = buffer[6];
                                                    break;
                                                case DataType.WORD:
                                                    value.Word = BitConverter.ToUInt16(buffer, 6);
                                                    break;
                                                case DataType.SHORT:
                                                    value.Int16 = BitConverter.ToInt16(buffer, 6);
                                                    break;
                                                case DataType.DWORD:
                                                    value.DWord = BitConverter.ToUInt32(buffer, 6);
                                                    break;
                                                case DataType.INT:
                                                    value.Int32 = BitConverter.ToInt32(buffer, 6);
                                                    break;
                                                case DataType.FLOAT:
                                                    value.Single = BitConverter.ToSingle(buffer, 6);
                                                    break;
                                                default:
                                                    break;
                                            }
                                            rs = (byte)tag.Write(value, false);
                                        }
                                    }
                                    else
                                    {
                                        rs = 0xFF;//此处长度应注意;如无此变量，应返回一个错误代码
                                    }
                                    s_Receive.Send(new byte[] { FCTCOMMAND.fctWriteSingle, rs }, 0, 2, SocketFlags.None);//应返回一个错误代码;
                                }
                                break;
                            case FCTCOMMAND.fctWriteMultiple:
                                {  //int BatchWrite(IDictionary<ITag, object> items, bool isSync = true);
                                    int count = BitConverter.ToInt16(buffer, 2);
                                    int j = 4; byte rs = 0;
                                    Dictionary<IGroup, SortedDictionary<ITag, object>> dict = new Dictionary<IGroup, SortedDictionary<ITag, object>>();
                                    for (int i = 0; i < count; i++)
                                    {
                                        short id = BitConverter.ToInt16(buffer, j);
                                        j += 2;
                                        byte length = buffer[j++];
                                        ITag tag = this[id];
                                        IGroup grp = tag.Parent;
                                        SortedDictionary<ITag, object> values;
                                        if (!dict.ContainsKey(grp))
                                        {
                                            values = new SortedDictionary<ITag, object>();
                                            dict.Add(grp, values);
                                        }
                                        else
                                            values = dict[grp];
                                        if (tag != null)
                                        {
                                            switch (tag.Address.VarType)
                                            {
                                                case DataType.BOOL:
                                                    values.Add(tag, BitConverter.ToBoolean(buffer, j));
                                                    break;
                                                case DataType.BYTE:
                                                    values.Add(tag, buffer[j]);
                                                    break;
                                                case DataType.WORD:
                                                    values.Add(tag, BitConverter.ToUInt16(buffer, j));
                                                    break;
                                                case DataType.SHORT:
                                                    values.Add(tag, BitConverter.ToInt16(buffer, j));
                                                    break;
                                                case DataType.DWORD:
                                                    values.Add(tag, BitConverter.ToUInt32(buffer, j));
                                                    break;
                                                case DataType.INT:
                                                    values.Add(tag, BitConverter.ToInt32(buffer, j));
                                                    break;
                                                case DataType.FLOAT:
                                                    values.Add(tag, BitConverter.ToSingle(buffer, j));
                                                    break;
                                                case DataType.STR:
                                                    values.Add(tag, Encoding.ASCII.GetString(buffer, j, length).Trim((char)0));
                                                    break;
                                            }
                                        }
                                        j += length;
                                    }
                                    foreach (var dev in dict)
                                    {
                                        if (dev.Key.BatchWrite(dev.Value) < 0) rs = 0xFF;
                                    }
                                    s_Receive.Send(new byte[] { FCTCOMMAND.fctWriteMultiple, rs }, 0, 2, SocketFlags.None);
                                }
                                break;
                            case FCTCOMMAND.fctAlarmRequest://刷新报警数据
                                break;
                            case FCTCOMMAND.fctReset://重置连接
                                {
                                    byte[] iparry = new byte[4];
                                    Array.Copy(buffer, 2, iparry, 0, 4);
                                    IPAddress ipaddr = new IPAddress(iparry);
                                    if (_socketThreadList.Count > 0 && _socketThreadList.ContainsKey(ipaddr))
                                    {
                                        var scok = _socketThreadList[ipaddr];
                                        _socketThreadList.Remove(ipaddr);
                                        if (scok != null)
                                        {
                                            scok.Dispose();
                                        }
                                    }
                                }
                                break;
                            case FCTCOMMAND.fctHdaRequest:
                            case FCTCOMMAND.fctHdaIdRequest://优先读取本地HDA文件夹下的二进制归档文件
                                break;
                        }
                    }
                }
                catch (SocketException ex)
                {
                    var err = ex.SocketErrorCode;
                    if (err == SocketError.ConnectionAborted || err == SocketError.HostDown || err == SocketError.NetworkDown || err == SocketError.Shutdown || err == SocketError.ConnectionReset)
                    {
                        s_Receive.Dispose();
                        if (addr != null)
                            _socketThreadList.Remove(addr);
                        //s_Receive.Dispose();
                    }
                    AddErrorLog(ex);
                }
                catch (Exception ex)
                {
                    AddErrorLog(ex);
                }
            }
        }


        public int BatchWrite(Dictionary<string, object> tags, bool sync)
        {
            int rs = -1;
            Dictionary<IGroup, SortedDictionary<ITag, object>> dict = new Dictionary<IGroup, SortedDictionary<ITag, object>>();
            foreach (var item in tags)
            {
                var tag = this[item.Key];
                if (tag != null)
                {
                    IGroup grp = tag.Parent;
                    SortedDictionary<ITag, object> values;
                    if (!dict.ContainsKey(grp))
                    {
                        values = new SortedDictionary<ITag, object>();
                        if (tag.Address.VarType != DataType.BOOL && tag.Address.VarType != DataType.STR)
                        {
                            values.Add(tag, tag.ValueToScale(Convert.ToSingle(item.Value)));
                        }
                        else
                            values.Add(tag, item.Value);
                        dict.Add(grp, values);
                    }
                    else
                    {
                        values = dict[grp];
                        if (tag.Address.VarType != DataType.BOOL && tag.Address.VarType != DataType.STR)
                        {
                            values.Add(tag, tag.ValueToScale(Convert.ToSingle(item.Value)));
                        }
                        else
                            values.Add(tag, item.Value);
                    }
                }
            }
            foreach (var dev in dict)
            {
                rs = dev.Key.BatchWrite(dev.Value, sync);
            }
            return rs;
        }

        void grp_DataChange(object sender, DataChangeEventArgs e)
        {
            var data = e.Values;
            var now = DateTime.Now;
            if (_socketThreadList != null && _socketThreadList.Count > 0)
            {
                IPAddress addr = null;
                var grp = sender as ClientGroup;
                if (grp != null)
                    addr = grp.RemoteAddress;
                ThreadPool.UnsafeQueueUserWorkItem(new WaitCallback(this.SendData), new TempCachedData(addr, data));
            }
        }
        //此处发生内存泄漏；需要试验CLRProfile确定泄漏原因；改回原方法测试；看是否解决队列堵塞问题。对于客户端Grp,要过滤掉
        private void SendData(object obj)
        {
            var tempdata = obj as TempCachedData;
            var data = tempdata.Data;
            byte[] sendBuffer = new byte[8192];
            sendBuffer[0] = FCTCOMMAND.fctHead;
            sendBuffer[1] = FCTCOMMAND.fctReadMultiple;
            //bytes[2] = 0;
            short j = 5;
            for (int i = 0; i < data.Count; i++)
            {
                short id = data[i].ID;
                var propid = GetItemProperties(id);
                if (propid >= 0 && propid < _list.Count)
                {
                    byte[] dt = BitConverter.GetBytes(id);
                    sendBuffer[j++] = dt[0];
                    sendBuffer[j++] = dt[1];
                    switch (_list[propid].DataType)
                    {
                        case DataType.BOOL:
                            sendBuffer[j++] = 1;
                            sendBuffer[j++] = data[i].Value.Boolean ? (byte)1 : (byte)0;
                            break;
                        case DataType.BYTE:
                            sendBuffer[j++] = 1;
                            sendBuffer[j++] = data[i].Value.Byte;
                            break;
                        case DataType.WORD:
                            {
                                sendBuffer[j++] = 2;
                                byte[] bt = BitConverter.GetBytes(data[i].Value.Word);
                                sendBuffer[j++] = bt[0];
                                sendBuffer[j++] = bt[1];
                            }
                            break;
                        case DataType.SHORT:
                            {
                                sendBuffer[j++] = 2;
                                byte[] bt = BitConverter.GetBytes(data[i].Value.Int16);
                                sendBuffer[j++] = bt[0];
                                sendBuffer[j++] = bt[1];
                            }
                            break;
                        case DataType.DWORD:
                            {
                                sendBuffer[j++] = 4;
                                byte[] bt = BitConverter.GetBytes(data[i].Value.DWord);
                                sendBuffer[j++] = bt[0];
                                sendBuffer[j++] = bt[1];
                                sendBuffer[j++] = bt[2];
                                sendBuffer[j++] = bt[3];
                            }
                            break;
                        case DataType.INT:
                            {
                                sendBuffer[j++] = 4;
                                byte[] bt = BitConverter.GetBytes(data[i].Value.Int32);
                                sendBuffer[j++] = bt[0];
                                sendBuffer[j++] = bt[1];
                                sendBuffer[j++] = bt[2];
                                sendBuffer[j++] = bt[3];
                            }
                            break;
                        case DataType.FLOAT:
                            {
                                sendBuffer[j++] = 4;
                                byte[] bt = BitConverter.GetBytes(data[i].Value.Single);
                                sendBuffer[j++] = bt[0];
                                sendBuffer[j++] = bt[1];
                                sendBuffer[j++] = bt[2];
                                sendBuffer[j++] = bt[3];
                            }
                            break;
                        case DataType.STR:
                            {
                                byte[] bt = Encoding.ASCII.GetBytes(this[data[i].ID].ToString());
                                sendBuffer[j++] = (byte)bt.Length;
                                for (int k = 0; k < bt.Length; k++)
                                {
                                    sendBuffer[j++] = bt[k];
                                }
                            }
                            break;
                        default:
                            break;
                    }
                    Array.Copy(BitConverter.GetBytes((data[i].TimeStamp == DateTime.MinValue ? DateTime.Now : data[i].TimeStamp).ToFileTime()), 0, sendBuffer, j, 8);
                    j += 8;
                }
            }
            byte[] dt1 = BitConverter.GetBytes(j);
            sendBuffer[3] = dt1[0];
            sendBuffer[4] = dt1[1];
            SocketError err;
            //bytes.CopyTo(bytes2, 0);
            List<Socket> sockets = new List<Socket>();
            foreach (var socket in _socketThreadList)
            {
                if (!socket.Key.Equals(tempdata.Address))
                    sockets.Add(socket.Value);
            }
            data = null;
            obj = null;
            tempdata = null;
            foreach (var socket in sockets)
            {
                try
                {
                    socket.Send(sendBuffer, 0, j, SocketFlags.None, out err);
                    if (err == SocketError.ConnectionAborted || err == SocketError.HostDown ||
                        err == SocketError.NetworkDown || err == SocketError.Shutdown)
                    {
                        _socketThreadList.Remove((socket.RemoteEndPoint as IPEndPoint).Address);
                    }
                }
                catch (Exception ex1)
                {
                    AddErrorLog(ex1);
                }
            }
        }
        public IDriver AddDriver1(short id, string name, string assembly, string className)
        {
            if (_drivers.ContainsKey(id))
                return _drivers[id];
            IDriver dv = null;
            switch (className)
            {
                case "FileDriver.TagDriver":
                    dv = new TagDriver(this,id,name);
                    _drivers.Add(id, dv);
                    return dv;
                case "ModbusDriver.ModbusTCPReader":
                    dv = new ModbusTCPReader(this, id, name);
                    _drivers.Add(id, dv);
                    return dv;
                case "ModbusDriver.ModbusRTUDriver":
                    dv = new ModbusRTUReader(this, id, name);
                    _drivers.Add(id, dv);
                    return dv;
                default:
                    return dv;
            }
        }
        public IDriver AddDriver(short id, string name, string assembly, string className)
        {
            if (_drivers.ContainsKey(id))
                return _drivers[id];
            IDriver dv = null;
            try
            {
                Assembly ass = Assembly.LoadFrom(assembly);
                var dvType = ass.GetType(className);
                if (dvType != null)
                {
                    dv = Activator.CreateInstance(dvType, new object[] { this, id, name }) as IDriver;
                    if (dv != null)
                    {
                        foreach (var arg in _arguments)
                        {
                            if (arg.DriverID == id)
                            {
                                var prop = dvType.GetProperty(arg.PropertyName);
                                if (prop != null)
                                {
                                    if (prop.PropertyType.IsEnum)
                                        prop.SetValue(dv, Enum.Parse(prop.PropertyType, arg.PropertyValue), null);
                                    else
                                        prop.SetValue(dv, Convert.ChangeType(arg.PropertyValue, prop.PropertyType, CultureInfo.CreateSpecificCulture("en-US")), null);
                                }
                            }
                        }
                        _drivers.Add(id, dv);
                    }
                }
            }
            catch (Exception e)
            {
                AddErrorLog(e);
            }
            return dv;
        }

        public bool RemoveDriver(IDriver device)
        {
            lock (SyncRoot)
            {
                if (_drivers.Remove(device.ID))
                {
                    device.Dispose();
                    device = null;
                    return true;
                }
                return false;
            }
        }

        void reader_OnClose(object sender, IOErrorEventArgs e)
        {
            //AddErrorLog(new Exception(e.shutdownReason));
        }

        public bool AddItemIndex(string key, ITag value)
        {
            key = key.ToUpper();
            if (_mapping.ContainsKey(key))
                return false;
            _mapping.Add(key, value);
            return true;
        }

        public bool RemoveItemIndex(string key)
        {
            return _mapping.Remove(key.ToUpper());
        }

        object _alarmsync = new object();

        string[] itemList = null;
        public IEnumerable<string> BrowseItems(BrowseType browseType, string tagName, DataType dataType)
        {
            lock (SyncRoot)
            {
                if (_list.Count == 0) yield break;
                int len = _list.Count;
                if (itemList == null)
                {
                    itemList = new string[len];
                    for (int i = 0; i < len; i++)
                    {
                        itemList[i] = _list[i].Name;
                    }
                    Array.Sort(itemList);
                }
                int ii = 0;
                bool hasTag = !string.IsNullOrEmpty(tagName);
                bool first = true;
                string str = hasTag ? tagName + SPLITCHAR : string.Empty;
                if (hasTag)
                {
                    ii = Array.BinarySearch(itemList, tagName);
                    if (ii < 0) first = false;
                    //int strLen = str.Length;
                    ii = Array.BinarySearch(itemList, str);
                    if (ii < 0) ii = ~ii;
                }
                //while (++i < len && temp.Length >= strLen && temp.Substring(0, strLen) == str)
                do
                {
                    if (first && hasTag)
                    {
                        first = false;
                        yield return tagName;
                    }
                    string temp = itemList[ii];
                    if (hasTag && !temp.StartsWith(str, StringComparison.Ordinal))
                        break;
                    if (dataType == DataType.NONE || _mapping[temp].Address.VarType == dataType)
                    {
                        bool b3 = true;
                        if (browseType != BrowseType.Flat)
                        {
                            string curr = temp + SPLITCHAR;
                            int index = Array.BinarySearch(itemList, ii, len - ii, curr);
                            if (index < 0) index = ~index;
                            b3 = itemList[index].StartsWith(curr, StringComparison.Ordinal);
                            if (browseType == BrowseType.Leaf)
                                b3 = !b3;
                        }
                        if (b3)
                            yield return temp;
                    }
                } while (++ii < len);
            }
        }

        public int GetScaleByID(short Id)
        {
            if (_scales == null || _scales.Count == 0) return -1;
            return _scales.BinarySearch(new Scaling { ID = Id });
        }

        public IGroup GetGroupByName(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            foreach (IDriver device in Drivers)
            {
                foreach (IGroup grp in device.Groups)
                {
                    if (grp.Name == name)
                        return grp;
                }
            }
            return null;
        }

        public void ActiveItem(bool active, params ITag[] items)
        {
            Dictionary<IGroup, List<short>> dict = new Dictionary<IGroup, List<short>>();
            for (int i = 0; i < items.Length; i++)
            {
                List<short> list = null;
                ITag item = items[i];
                dict.TryGetValue(item.Parent, out list);
                if (list != null)
                {
                    list.Add(item.ID);
                }
                else
                    dict.Add(item.Parent, new List<short> { item.ID });

            }
            foreach (var grp in dict)
            {
                grp.Key.SetActiveState(active, grp.Value.ToArray());
            }
        }

        public int GetItemProperties(short id)
        {
            return _list.BinarySearch(new TagMetaData { ID = id });
        }
        #endregion

    }

  

    class TempCachedData
    {
        IPAddress _addr;
        public IPAddress Address
        {
            get { return _addr; }
        }

        IList<HistoryData> _data;
        public IList<HistoryData> Data
        {
            get { return _data; }
        }

        public TempCachedData(IPAddress addr, IList<HistoryData> data)
        {
            _addr = addr;
            _data = data;
        }
    }

    internal sealed class ArchiveTime
    {
        public int Cycle;
        public DateTime LastTime;
        public ArchiveTime(int cycle, DateTime last)
        {
            Cycle = cycle;
            LastTime = last;
        }
    }
}
