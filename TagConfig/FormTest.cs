using DataService;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TagConfig
{
    public partial class FormTest : Form
    {
        public FormTest()
        {
            InitializeComponent();
        }
        Dictionary<int, string> dic = new Dictionary<int, string>();
        List<model> models = new List<model>();
        private void button1_Click(object sender, EventArgs e)
        {
            Array ayy = Enum.GetValues(typeof(DataType));
            for (int i = 0; i < ayy.Length; i++)
            {
                model m = new model() { Type = (DataType)ayy.GetValue(i) };
                models.Add(m);
            }
            dataGridView1.DataSource = models;
        }

        private void FormTest_Load(object sender, EventArgs e)
        {
            Array values = Enum.GetValues(typeof(DataType));
            string[] names = Enum.GetNames(typeof(DataType));
            for (int i = 0; i < values.Length; i++)
            {
                DataType fuck = (DataType)values.GetValue(i);
                int key = (int)fuck;
                dic.Add(key, names[i]);
            }
            Column1.DisplayMember = "Key";
            Column1.ValueMember = "Value";
        }
    }
    public class model
    {
        public DataType Type { set; get; }
    }
}
