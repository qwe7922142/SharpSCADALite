using ClientDriver;
using DataService;
using FileDriver;
using ModbusDriver;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Timers;
using Newtonsoft.Json.Linq;
using System.IO.Ports;

namespace CoreExample
{
    public class DAService : IDataServer
    {
        const int PORT = 6543;
        const char SPLITCHAR = '.';

        //可配置参数，从XML文件读取
        int CYCLE = 60000;
        private System.Timers.Timer timer1 = new System.Timers.Timer();

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

        Socket tcpServer = null;

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
            //InitHost();
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

                        if (tcpServer != null && tcpServer.Connected)
                        {
                            tcpServer.Disconnect(false);
                            tcpServer.Dispose();
                        }

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
                AddDriver1(model.ID, model.Name, model.Assembly, model.ClassName,(JObject) model.Target);
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
        //todo:这里改成tag的update方法就省去了通信
        void grp_DataChange(object sender, DataChangeEventArgs e)
        {
            var data = e.Values;
            var now = DateTime.Now;
        }
        public IDriver AddDriver1(short id, string name, string assembly, string className, JObject target)
        {
            if (_drivers.ContainsKey(id))
                return _drivers[id];
            switch (className)
            {
                case "FileDriver.TagDriver":
                    TagDriver dv1 = new TagDriver(this,id,name);
                    _drivers.Add(id, dv1);
                    return dv1;
                case "ModbusDriver.ModbusTCPReader"://todo:这里的实现有点挫，后面有空改进
                    ModbusTCPReader dv2 = new ModbusTCPReader(this, id, name);
                    dv2.ServerName = target["ServerName"].ToString();
                    dv2.Port =int.Parse( target["Port"].ToString());
                    dv2.TimeOut = int.Parse(target["TimeOut"].ToString());
                    dv2.SlaveID = short.Parse(target["SlaveID"].ToString());
                    _drivers.Add(id, dv2);
                    return dv2;
                case "ModbusDriver.ModbusRTUReader":
                    ModbusRTUReader dv3 = new ModbusRTUReader(this, id, name);
                    dv3.PortName = target["PortName"].ToString();
                    dv3.TimeOut = int.Parse(target["TimeOut"].ToString());
                    dv3.SlaveID = short.Parse(target["SlaveID"].ToString());
                    dv3.BaudRate = int.Parse(target["BaudRate"].ToString());
                    dv3.DataBits = int.Parse(target["DataBits"].ToString());
                    dv3.StopBits =(StopBits) int.Parse(target["StopBits"].ToString());
                    dv3.Parity = (Parity)int.Parse(target["Parity"].ToString());
                    _drivers.Add(id, dv3);
                    return dv3;
                default:
                    return null;
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

    }
   
}
