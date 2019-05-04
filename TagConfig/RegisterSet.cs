using DataService;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using System.Windows.Forms;
using System.Linq;
namespace TagConfig
{
    public partial class RegisterSet : Form
    {
        Type driverType;
        public RegisterSet()
        {
            InitializeComponent();
        }

        private void Register_Load(object sender, EventArgs e)
        {
            try
            {
                Assembly dsass = Assembly.LoadFrom(@"DataService.dll");
                driverType = dsass.GetType("DataService.IDriver");
            }
            catch (Exception ex)
            {
                Program.AddErrorLog(ex);
            }
        }

        private void txtPath_DoubleClick(object sender, EventArgs e)
        {
            string path = txtPath.Text;
            if (!string.IsNullOrEmpty(path))
            {
                int index = path.LastIndexOf('\\');
                if (index < 0)
                {
                    openFileDialog1.FileName = path;
                }
                else
                {
                    openFileDialog1.InitialDirectory = path.Substring(0, index);
                    openFileDialog1.FileName = path.Substring(index + 1, path.Length - index - 1);
                }
            }
            openFileDialog1.Filter = "dll文件 (*.dll)|*.dll|All files (*.*)|*.*";
            openFileDialog1.DefaultExt = "dll";
            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                List<RegisterModule> regList = new List<RegisterModule>();
                string file = openFileDialog1.FileName;
                txtPath.Text = file;
                try
                {
                    if (driverType != null)
                    {
                        Assembly ass = Assembly.LoadFrom(file);
                        foreach (Type type in ass.GetTypes())
                        {
                            if (driverType.IsAssignableFrom(type))
                            {
                                string attribute = null;
                                foreach (var attr in type.GetCustomAttributes(false))
                                {
                                    DescriptionAttribute desp = attr as DescriptionAttribute;
                                    if (desp != null)
                                    {
                                        attribute = desp.Description;
                                    }
                                }
                                //regList.Add(new RegisterModule(file, type.Name, type.FullName, attribute));
                                regList.Add(new RegisterModule() { AssemblyName = file,ClassName = type.Name,ClassFullName = type.FullName,Description = attribute});

                            }
                        }
                    }
                    bindingSource1.DataSource = new SortableBindingList<RegisterModule>(regList);
                }
                catch (Exception ex)
                {
                    Program.AddErrorLog(ex);
                }
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
          //var source=  bindingSource1.DataSource as IEnumerable<RegisterModule>;
          //  var regModules = DataHelper.GetDriverMetaDataByJson();
          //  var driverMetas = DataHelper.GetDriverMetaDataByJson();
          //  if (source != null)
          //  {
          //      short lastID = driverMetas.Select(a => a.ID).Max();
          //      foreach (var reg in source)
          //      {
          //          lastID++;
          //          driverMetas.RemoveAll(a => a.ClassName == reg.ClassFullName);
          //          driverMetas.Add(new DriverMetaData() { ID = lastID,DriverID= reg.DriverID,Name=reg.n});
          //      }
          //      //if (DataHelper.Instance.ExecuteNonQuery(sb.ToString()) >= 0)
          //      //    MessageBox.Show("注册成功！");
          //  }
        }
    }
}
