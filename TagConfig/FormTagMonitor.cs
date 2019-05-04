using CoreExample;
using DataService;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Forms;

namespace TagConfig
{
    public partial class FormTagMonitor : Form
    {
        DAService service;
        private int groupID;
        ObservableCollection<TagItem> list = new ObservableCollection<TagItem>();
        public FormTagMonitor(DAService service,int groupID)
        {
            InitializeComponent();
            this.service = service;
            this.groupID = groupID;
        }

        private void FormTagMonitor_Load(object sender, EventArgs e)
        {
            try
            {
                var metalist = service.MetaDataList.Where(a => a.GroupID == groupID).ToList();
                for (int i = 0; i < metalist.Count; i++)
                {
                    list.Add(new TagItem(metalist[i], service[metalist[i].Name], dgvPlc, i));
                }
                dgvPlc.DataSource = list;
            }
            catch (Exception ex)
            {

                throw;
            }
        }
    }
    public class TagItem:IDisposable
    {
        ITag _tag;
        private DataGridView _dataGrid;
        private int _index;
        string _tagname;
        public string TagName
        {
            get { return _tagname; }
            set { _tagname = value; }
        }

        string _addr;
        public string Address
        {
            get { return _addr; }
            set { _addr = value; }
        }

        string _tagValue;
        public string TagValue
        {
            get { return _tagValue; }
            set
            {
                if (_tagValue != value)
                {
                    _tagValue = value;
                }
            }
        }

        DateTime _timestamp;
        public DateTime TimeStamp
        {
            get { return _timestamp; }
            set
            {
                if (_timestamp != value)
                {
                    _timestamp = value;
                }
            }
        }

        public TagItem(TagMetaData metadata,ITag tag,DataGridView dataGrid,int index)
        {
            _tagname = metadata.Name;
            _addr = metadata.Address;
            _tag = tag;
            _dataGrid = dataGrid;
            _index = index;
            if (_tag != null)
            {
                _tagValue = _tag.ToString();
                _timestamp = _tag.TimeStamp;
                _tag.ValueChanged += new ValueChangedEventHandler(TagValueChanged);
            }
        }

        private void TagValueChanged(object sender, ValueChangedEventArgs args)
        {
            TagValue = _tag.ToString();
            TimeStamp = _tag.TimeStamp;
            _dataGrid.Invoke(new MethodInvoker(() =>
            {
                _dataGrid.InvalidateRow(_index);
            }));
        }

        public int Write(string value)
        {
            if (string.IsNullOrEmpty(value)) return -1;
            if (_tag.Address.VarType == DataType.BOOL)
            {
                if (value == "1") value = "true";
                if (value == "0") value = "false";
            }
            return _tag.Write(value);
        }

        public void SimWrite(string value)
        {
            if (string.IsNullOrEmpty(value)) return;
            Storage stor = Storage.Empty;
            try
            {
                if (_tag.Address.VarType == DataType.STR)
                {
                    ((StringTag)_tag).String = value;
                }
                else
                {
                    stor = _tag.ToStorage(value);
                }
                _tag.Update(stor, DateTime.Now, QUALITIES.QUALITY_GOOD);
            }
            catch { }
        }

        public void Dispose()
        {
            if (_tag != null)
            {
                _tag.ValueChanged -= new ValueChangedEventHandler(TagValueChanged);
            }
        }
    }
}
