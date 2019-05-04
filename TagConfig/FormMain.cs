//using Microsoft.Office.Interop.Excel;
using CoreExample;
using DataService;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Xml;
//using Excel = Microsoft.Office.Interop.Excel;

namespace TagConfig
{
    //可考虑支持EXCEL文件的导入导出
    public partial class FormMain : Form
    {
        const string FILENAME = "meta.xml";

        bool start = false;
        string file = null;
        TreeNode majorTop;
        bool isCut = false;
        short curgroupId = 0;
        List<DriverMetaData> devices = new List<DriverMetaData>();
        List<GroupMeta> groups = new List<GroupMeta>();
        List<TagMetaData> list = new List<TagMetaData> ();
        //List<short> indexList = new List<short>();
        List<Scaling> scaleList = new List<Scaling>();
        List<TagMetaData> selectedTags = new List<TagMetaData>();
        List<RegisterModule> typeList = new List<RegisterModule>();
        List<DriverArgumet> arguments = new List<DriverArgumet>();

        SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder();

        public static readonly List<DataTypeSource> DataDict = new List<DataTypeSource>
        {
           new DataTypeSource (1,"开关型"),new DataTypeSource (3,"字节"), new DataTypeSource (4,"短整型"),
           new DataTypeSource (5,"单字型"),new DataTypeSource (6,"双字型"),new DataTypeSource (7,"长整型"),
           new DataTypeSource (8,"浮点型"),new DataTypeSource (9,"系统型"),new DataTypeSource (10,"ASCII字符串"),
           new DataTypeSource(0,"")
        };


        public FormMain()
        {
            InitializeComponent();
            DataGridViewComboBoxColumn col = dataGridView1.Columns["Column3"] as DataGridViewComboBoxColumn;
            col.DataSource = DataDict;
            col.DisplayMember = "Name";
            col.ValueMember = "DataType";
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            majorTop = treeView1.Nodes.Add("", "服务器", 0, 0);
            LoadFromDatabase();
            treeView1.ExpandAll();
        }

        private void LoadFromDatabase()
        {
            list.Clear();
            //subConds.Clear();
            majorTop.Nodes.Clear();
            devices = DataHelper.GetDriverMetaDataByJson();
            foreach (var item in devices)
            {
                majorTop.Nodes.Add(item.ID.ToString(), item.Name, 1, 1);
            }
            List<GroupMeta> temps = DataHelper.GetGroupMetaByJson();
            foreach (TreeNode node in majorTop.Nodes)
            {
                List<GroupMeta> gps = temps.FindAll(a=>a.DriverID.ToString()== node.Name);
                foreach (var item in gps)
                {
                    groups.Add(item);
                    node.Nodes.Add(item.ID.ToString(), item.Name, 2, 2);
                }
            }
            list = DataHelper.GetTagMetaDataByJson();
            typeList = DataHelper.GetRegisterModuleByJson();
            arguments = DataHelper.GetDriverArgumetByJson();
          
            list.Sort();
            start = true;
        }

        private bool Save()
        {
            DataHelper.SaveDriverArgumetByJson(arguments);
            DataHelper.SaveDriverMetaDataByJson(devices);
            DataHelper.SaveTagMetaDataByJson(list);
            DataHelper.SaveGroupMetaByJson(groups);
            DataHelper.SaveRegisterModuleByJson(typeList);
            return true;

        }


        public static readonly Dictionary<string, int> severitys = new Dictionary<string, int>
        {
             {"Infomations",0},{"Messages",1},{"Warnings",1},{"LO",2},{"MidLO",3},{"Mid",4},{"MidHI",5},{"HI",6},{"Errors",7}
        };

        private void treeView1_AfterSelect(object sender, TreeViewEventArgs e)
        {
            if (!start) return;
            List<TagMetaData> data = new List<TagMetaData>();
            switch (e.Node.Level)
            {
                case 0:
                    data = list;
                    break;
                case 1:
                    {
                        foreach (TreeNode node in e.Node.Nodes)
                        {
                            curgroupId = short.Parse(node.Name);
                            int index = list.FindIndex(a => a.GroupID == curgroupId);
                            //int index = list.BinarySearch(new TagMetaData(curgroupId, null));
                            if (index < 0) index = ~index;
                            if (index < list.Count)
                                data = list.Where(a => a.GroupID == curgroupId).ToList();
                        }
                    }
                    break;
                case 2:
                    {
                        curgroupId = short.Parse(e.Node.Name);
                        int index = list.FindIndex(a => a.GroupID == curgroupId);
                        //int index = list.BinarySearch(new TagMetaData(curgroupId, null));
                        if (index < 0) index = ~index;
                        if (index < list.Count)
                            data = list.Where(a => a.GroupID == curgroupId).ToList();
                    }
                    break;
            }
            bindingSource1.DataSource = new SortableBindingList<TagMetaData>(data);
            tspCount.Text = data.Count.ToString();
        }

        public void FindNode(string filter)
        {
            foreach (TreeNode tr in treeView1.Nodes)
            {
                TreeFind(tr, filter);
            }
        }

        private void TreeFind(TreeNode node, string filter)
        {
            if (node.Text == filter)
            {
                treeView1.SelectedNode = node;
                return;
            }
            foreach (TreeNode tn in node.Nodes)
            {
                TreeFind(tn, filter);
            }
        }

        public void AddNode()
        {
            TreeNode node = treeView1.SelectedNode;
            if (node != null)
            {
                short did = 0;// short.MinValue;
                if (node.Level == 0)
                {
                    for (int i = 0; i < devices.Count; i++)
                    {
                        short temp = devices[i].ID;
                        if (temp > did)
                            did = temp;
                    }
                    did++;
                    devices.Add(new DriverMetaData { ID = did });
                }
                else if (node.Level == 1)
                {
                    for (int i = 0; i < groups.Count; i++)
                    {
                        short temp = groups[i].ID;
                        if (temp > did)
                            did = temp;
                    }
                    did++;
                    groups.Add(new GroupMeta { ID = did, DriverID = short.Parse(node.Name) });
                }
                else if (node.Level == 2)
                {
                    AddTag();
                    return;
                }
                TreeNode nwNode = node.Nodes.Add(did.ToString(), "", node.Level + 1, node.Level + 1);
                treeView1.SelectedNode = nwNode;
                treeView1.LabelEdit = true;
                nwNode.BeginEdit();
                //bindingSource1.Clear();
            }
        }

        public void UpdateNode()
        {
            TreeNode node = treeView1.SelectedNode;
            if (node != null && node.Level != 0)
            {
                treeView1.LabelEdit = true;
                node.BeginEdit();
            }
            else
                treeView1.LabelEdit = false;
        }

        public void RemoveNode()
        {
            TreeNode node = treeView1.SelectedNode;
            if (node != null && ((node.Level == 2 && bindingSource1.Count == 0)
                || (node.Level == 1 && node.Nodes.Count == 0)))
            {
                if (node.Level == 1)
                {
                    foreach (DriverMetaData device in devices)
                    {
                        if (device.ID.ToString() == node.Name)
                        {
                            foreach (GroupMeta grp in groups)
                            {
                                if (grp.DriverID.ToString() == node.Name)
                                {
                                    groups.Remove(grp);
                                    node.Remove();
                                    return;
                                }
                            }
                            devices.Remove(device);
                            node.Remove();
                            return;
                        }
                    }
                }
                else
                {
                    foreach (GroupMeta grp in groups)
                    {
                        if (grp.ID.ToString() == node.Name)
                        {
                            groups.Remove(grp);
                            node.Remove();
                            return;
                        }
                    }
                }
            }
            else
            {
                MessageBox.Show("包含下级，禁止删除!");
            }
        }

        private void treeView1_AfterLabelEdit(object sender, NodeLabelEditEventArgs e)
        {
            if (!start) return;
            if (string.IsNullOrEmpty(e.Label))
            {
                e.CancelEdit = false;
            }
            else
            {
                treeView1.LabelEdit = false;
                if (e.Node.Level == 1)
                {
                    foreach (DriverMetaData device in devices)
                    {
                        if (device.ID.ToString() == e.Node.Name)
                        {
                            device.Name = e.Label;
                            break;
                        }
                    }
                }
                else
                {
                    if (!groups.Exists(x => x.Name == e.Label))
                    {
                        foreach (GroupMeta grp in groups)
                        {
                            if (grp.ID.ToString() == e.Node.Name)
                            {
                                grp.Name = e.Label;
                                break;
                            }
                        }
                    }
                    else
                    {
                        MessageBox.Show("组名不能重复!");
                    }
                }
            }
        }

        IEnumerable<int> GetTagNames(string filter)
        {
            if (bindingSource1.Count == 0) yield break;
            int index = -1;
            foreach (TagMetaData tag in bindingSource1.DataSource as IEnumerable<TagMetaData>)
            {
                index++;
                if (string.IsNullOrEmpty(tag.Name))
                    continue;
                else if (tag.Name.ToUpper().Contains(filter))
                {
                    yield return index;
                }
            }
        }

        IEnumerable<int> GetTags(string filter)
        {
            if (bindingSource1.Count == 0) yield break;
            int index = -1;
            foreach (TagMetaData tag in bindingSource1.DataSource as IEnumerable<TagMetaData>)
            {
                index++;
                if (string.IsNullOrEmpty(tag.Description))
                    continue;
                else if (tag.Description.Contains(filter))
                {
                    yield return index;
                }
            }
        }

        private void AddTag()
        {
            TagMetaData tag = new TagMetaData((short)(list.Count == 0 ? 1 : list.Max(x => x.ID) + 1), short.Parse(treeView1.SelectedNode.Name),"","",DataType.INT,1);
            bindingSource1.Add(tag);
            int index = list.BinarySearch(tag);
            if (index < 0) index = ~index;
            list.Insert(index, tag);
            dataGridView1.FirstDisplayedScrollingRowIndex = bindingSource1.Count - 1;
        }

        private void toolStrip1_ItemClicked(object sender, ToolStripItemClickedEventArgs e)
        {
            switch (e.ClickedItem.Text)
            {
                case "增加":
                    if (treeView1.SelectedNode != null && treeView1.SelectedNode.Level == 2)
                    {
                        AddTag();
                    }
                    break;
                case "删除":
                    {
                        TagMetaData tag = bindingSource1.Current as TagMetaData;
                        bindingSource1.Remove(tag);
                        list.Remove(tag);
                    }
                    break;
                case "清除":
                    if (MessageBox.Show("将清除所有的标签，是否确定？", "警告", MessageBoxButtons.YesNo) == DialogResult.Yes)
                    {
                        bindingSource1.Clear();
                        list.Clear();
                    }
                    break;
                case "保存":
                    if (Save())
                        MessageBox.Show("保存成功!");
                    break;

                case "注册":
                    {
                        RegisterSet frm = new RegisterSet();
                        frm.ShowDialog();
                    }
                    break;
                case "查找":
                    {
                        string filter = txtFind.Text.ToUpper();
                        for (int i = 0; i < dataGridView1.Rows.Count; i++)
                        {
                            if (dataGridView1[0, i].Value.ToString() == filter)
                            {
                                dataGridView1.Rows[i].Selected = true;
                                dataGridView1.FirstDisplayedScrollingRowIndex = i;
                                return;
                            }
                        }
                    }
                    break;
                case "名称过滤":
                    {
                        dataGridView1.ClearSelection();
                        string filter = txtFilter.Text.ToUpper();
                        var tags = GetTagNames(filter);
                        if (tags != null)
                        {
                            foreach (int index in tags)
                            {
                                dataGridView1.Rows[index].Selected = true;
                                dataGridView1.FirstDisplayedScrollingRowIndex = index;
                            }
                        }
                    }
                    break;
                case "描述过滤":
                    {
                        dataGridView1.ClearSelection();
                        string filter = txtFilter1.Text.ToUpper();
                        var tags = GetTags(filter);
                        if (tags != null)
                        {
                            foreach (int index in tags)
                            {
                                dataGridView1.Rows[index].Selected = true;
                                dataGridView1.FirstDisplayedScrollingRowIndex = index;
                            }
                        }
                    }
                    break;
                case "退出":
                    this.Close();
                    break;
            }
            tspCount.Text = bindingSource1.Count.ToString();
        }

        private void toolStrip2_ItemClicked(object sender, ToolStripItemClickedEventArgs e)
        {
            switch (e.ClickedItem.Text)
            {
                case "导入变量":
                    openFileDialog1.Filter = "xml文件 (*.xml)|*.xml|excel文件 (*.xlsx)|*.xlsx|kepserver文件 (*.csv)|*.csv|All files (*.*)|*.*";
                    openFileDialog1.DefaultExt = "xml";
                    if (openFileDialog1.ShowDialog() == DialogResult.OK)
                    {
                        string file = openFileDialog1.FileName;
                        switch (openFileDialog1.FilterIndex)
                        {
                            case 1:
                                break;
                            case 2:
                                //LoadFromExcel(file);
                                break;
                            case 3:
                                //LoadFromKepserverCSV(file);
                                break;
                        }
                    }
                    break;
                case "导出变量":
                    saveFileDialog1.Filter = "xml文件 (*.xml)|*.xml|csv文件 (*.csv)|*.csv|All files (*.*)|*.*";
                    saveFileDialog1.DefaultExt = "xml";
                    if (saveFileDialog1.ShowDialog() == DialogResult.OK)
                    {
                        string file = saveFileDialog1.FileName;
                        switch (saveFileDialog1.FilterIndex)
                        {
                            case 1:
                                break;
                            case 2:
                                break;
                        }
                    }
                    break;
                case "导入报警":
                    {
                        openFileDialog1.Filter = "excel文件 (*.xlsx)|*.xlsx|All files (*.*)|*.*";
                        openFileDialog1.DefaultExt = "xlsx";
                        if (openFileDialog1.ShowDialog() == DialogResult.OK)
                        {
                            string file = openFileDialog1.FileName;
                            //LoadAlarmFromExcel(file);
                        }
                    }
                    break;
            }
        }

        private void toolStrip3_ItemClicked(object sender, ToolStripItemClickedEventArgs e)
        {
            switch (e.ClickedItem.Text)
            {
                case "前缀":
                    {
                        string front = txtFront.Text;
                        if (treeView1.SelectedNode.Level == 0)
                        {
                            foreach (var item in list)
                            {
                                item.Name = front + item.Name;
                            }
                        }
                        else if (treeView1.SelectedNode.Level == 1)
                        {
                            var rows = dataGridView1.Rows;
                            if (rows != null)
                            {
                                for (int i = 0; i < rows.Count; i++)
                                {
                                    TagMetaData tag = rows[i].DataBoundItem as TagMetaData;
                                    tag.Name = front + tag.Name;
                                }
                            }
                        }
                        else if (treeView1.SelectedNode.Level == 2)
                        {
                            foreach (var item in list.FindAll(x => x.GroupID == curgroupId))
                            {
                                item.Name = front + item.Name;
                            }
                        }
                    }
                    break;
                case "替换地址":
                    {
                        string filter = txtReplaceAddr1.Text;
                        string replace = txtReplaceAddr2.Text;
                        foreach (var item in list.FindAll(x => x.GroupID == curgroupId))
                        {
                            item.Address = item.Address.Replace(filter, replace);
                        }
                    }
                    break;
                case "偏移":
                    {
                        string offset = tspOffset.Text;
                        int baseaddr;
                        if (int.TryParse(offset, out baseaddr))
                        {
                            foreach (DataGridViewRow item in dataGridView1.SelectedRows)
                            {
                                var tag = item.DataBoundItem as TagMetaData;
                                var index = tag.Address.LastIndexOf('W');
                                if (index < 0)
                                {
                                    index = tag.Address.LastIndexOf('D');
                                }
                                if (index >= 0)
                                {
                                    var ad = Convert.ToDecimal(tag.Address.Substring(index + 1));
                                    ad = ad + baseaddr;
                                    tag.Address = tag.Address.Substring(0, index + 1) + ad.ToString();
                                }
                            }
                        }
                    }
                    break;
                case "替换名称":
                    {
                        string filter = txtReplace1.Text;
                        string replace = txtReplace2.Text;
                        foreach (var item in list.FindAll(x => x.GroupID == curgroupId))
                        {
                            item.Name = item.Name.Replace(filter, replace);
                        }
                    }
                    break;
            }
        }

        private void contextMenuStrip1_ItemClicked(object sender, ToolStripItemClickedEventArgs e)
        {
            switch (e.ClickedItem.Text)
            {
                case "增加":
                    AddNode();
                    break;
                case "删除":
                    RemoveNode();
                    break;
                case "重命名":
                    UpdateNode();
                    break;
                case "参数设置":
                    {
                        TreeNode node = treeView1.SelectedNode;
                        if (node != null)
                        {
                            if (node.Level == 1)
                            {
                                short id = short.Parse(node.Name);
                                foreach (DriverMetaData device in devices)
                                {
                                    if (device.ID == id)
                                    {
                                        DriverSet frm = new DriverSet(device, typeList, arguments);
                                        frm.ShowDialog();
                                        node.Text = device.Name;
                                        return;
                                    }
                                }
                            }
                            if (node.Level == 2)
                            {
                                short id = short.Parse(node.Name);
                                foreach (GroupMeta grp in groups)
                                {
                                    if (grp.ID == id)
                                    {
                                        GroupParam frm = new GroupParam(grp);
                                        frm.ShowDialog();
                                        node.Text = grp.Name;
                                        return;
                                    }
                                }
                            }
                        }
                    }
                    break;
            }
        }

        private void contextMenuStrip2_ItemClicked(object sender, ToolStripItemClickedEventArgs e)
        {
            switch (e.ClickedItem.Text)
            {
                case "变量编辑":
                    {
                        if (bindingSource1.Count > 0)
                        {
                            TagParam frm = new TagParam(bindingSource1);
                            frm.ShowDialog();
                        }
                    }
                    break;
                case "复制":
                    {
                        selectedTags.Clear();
                        var rows = dataGridView1.SelectedRows;
                        if (rows != null)
                        {
                            short maxid = list.Max(x => x.ID);
                            for (int i = 0; i < rows.Count; i++)
                            {
                                TagMetaData tag = rows[i].DataBoundItem as TagMetaData;
                                TagMetaData newtag = new TagMetaData((short)++maxid, tag.GroupID, tag.Name, tag.Address, tag.DataType,
                                    tag.Size,tag.Active,tag.Maximum, tag.Minimum, tag.Cycle);
                                selectedTags.Add(newtag);
                            }
                        }
                        isCut = false;
                    }
                    break;
                case "剪切":
                    {
                        selectedTags.Clear();
                        var rows = dataGridView1.SelectedRows;
                        if (rows != null)
                        {
                            for (int i = 0; i < rows.Count; i++)
                            {
                                TagMetaData tag = rows[i].DataBoundItem as TagMetaData;
                                selectedTags.Add(tag);
                            }
                        }
                        isCut = true;
                    }
                    break;
                case "粘贴CSV":
                    break;
                case "粘帖":
                    {
                        if (treeView1.SelectedNode == null || treeView1.SelectedNode.Level != 2)
                            return;
                        if (isCut)
                        {
                            foreach (var tag in selectedTags)
                            {
                                tag.GroupID = curgroupId;
                                bindingSource1.Add(tag);
                            }
                        }
                        else
                        {
                            foreach (var tag in selectedTags)
                            {
                                tag.GroupID = curgroupId;
                                bindingSource1.Add(tag);
                                list.Add(tag);
                            }
                        }
                        if (bindingSource1.Count > 0)
                        {
                            dataGridView1.FirstDisplayedScrollingRowIndex = bindingSource1.Count - 1;
                            list.Sort();
                        }
                        selectedTags.Clear();
                    }
                    break;
                case "批量删除":
                    {
                        var rows = dataGridView1.SelectedRows;
                        if (rows != null)
                        {
                            for (int i = 0; i < rows.Count; i++)
                            {
                                TagMetaData tag = rows[i].DataBoundItem as TagMetaData;
                                bindingSource1.Remove(tag);
                                list.Remove(tag);
                            }
                        }
                    }
                    break;
            }
            tspCount.Text = bindingSource1.Count.ToString();
        }


        private void button1_Click(object sender, EventArgs e)
        {
            TreeFind(majorTop, textBox1.Text);
        }

        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            if (MessageBox.Show("退出之前是否需要保存？", "警告", MessageBoxButtons.OKCancel) == DialogResult.OK)
            {
                Save();
            }
        }

        public void SplitAddress(string ad, ref int offset, ref int bit)
        {
            if (string.IsNullOrEmpty(ad))
            {
                offset = bit = 0;
                return;
            }
            string[] ads = ad.Split('.');
            offset = int.Parse(ads[0]);
            if (ads.Length > 1)
                bit = int.Parse(ads[1]);
        }
        DAService service;

        private void toolStripButton3_Click(object sender, EventArgs e)
        {
            if (tspStartService.Text == "关闭服务")
            {
                service.Dispose();
                service = null;
                tspStartService.Text = "开启服务";
            }
            else
            {
                service = new DAService();
                tspStartService.Text = "关闭服务";
            }
           
        }

        private void tspTagMonitor_Click(object sender, EventArgs e)
        {
            if (!start) return;
            List<TagMetaData> data = new List<TagMetaData>();
            FormTagMonitor frm = new FormTagMonitor(this.service, curgroupId);
            frm.Show();
        }

        private void tspConvert_Click(object sender, EventArgs e)
        {
            //try
            //{
            //    int id = 0;
            //    foreach (var item in list)
            //    {
            //        if (item.GroupID == 20002 && item.DataType == DataType.BOOL)
            //        {
            //            item.Address = (id).ToString("00000");
            //            item.GroupID = 20004;
            //            id++;
            //        }
            //    }
            //    DataHelper.SaveTagMetaDataByJson(list);
            //}
            //catch (Exception ex)
            //{

            //}

            try
            {
                FileStream fs = new FileStream(@"C:\Users\Administrator\Desktop\1.mbs", FileMode.Open, FileAccess.Read);
                BinaryReader br = new BinaryReader(fs);
                StreamWriter sw = new StreamWriter(@"C:\Users\Administrator\Desktop\1_1.mbs");
                int length = (int)fs.Length;
                List<byte> list = new List<byte>();
                while (length > 0)
                {
                    list.Add(br.ReadByte());
                    //string tempStr = Convert.ToString(tempByte, 16);
                    //if (temStr.Length == 1) temStr = "0" + temStr;
                    //sw.Write(tempStr);
                    length--;
                }
                int index = 0;
                List<int> ll = new List<int>();
                while (index< fs.Length-4)
                {
                    if (list[index] == 0 && list[index + 1] == 0 && list[index + 2] == 0 && list[index + 3] == 1)
                        ll.Add(index);
                    index++;
                }
                fs.Close();
                br.Close();
                sw.Close();
            }
            catch (Exception ex)
            {

                throw;
            }
        }
    }


    public class DataTypeSource
    {
        byte _type;
        public byte DataType { get { return _type; } set { _type = value; } }

        string _name;
        public string Name { get { return _name; } set { _name = value; } }

        public DataTypeSource(byte type, string name)
        {
            _type = type;
            _name = name;
        }
    }

   

}
