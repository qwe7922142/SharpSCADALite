using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using DataService;
using HMIControl;
using Microsoft.Research.DynamicDataDisplay.DataSources;
using FORMS = System.Windows.Forms;

namespace CoreTest
{
    public static class WindowHelper
    {
        //绑定到DAServer,采用递归
        public static List<TagNodeHandle> BindingToServer(this DependencyObject panel, IDataServer _srv)
        {
            if (_srv == null) return null;
            ExpressionEval eval = _srv.Eval;
            List<TagNodeHandle> valueChangedList = new List<TagNodeHandle>();
            var items = panel.FindTagControls();
            if (items != null)
            {
                foreach (var element in items)
                {
                    BindingControl(element, valueChangedList, eval);
                }
            }
            eval.Clear();
            valueChangedList.Sort();
            return valueChangedList;
        }

        private static void BindingControl(ITagLink taglink, List<TagNodeHandle> valueChangedList, ExpressionEval eval)
        {
            var ctrl = taglink as UIElement;
            if (ctrl == null) return;
            var complex = taglink as ITagReader;
            if (complex != null)
            {
                string txt = complex.TagReadText;
                if (!string.IsNullOrEmpty(txt))
                {
                    foreach (var v in txt.GetListFromText())
                    {
                        ITagLink tagConn = complex;
                        string[] strs = v.Key.Split('.');
                        if (strs.Length > 1)
                        {
                            for (int i = 0; i < strs.Length - 1; i++)
                            {
                                var c = tagConn as ITagReader;
                                if (c == null || c.Children == null) break;
                                foreach (var item in c.Children)
                                {
                                    if (item.Node == strs[i])
                                    {
                                        tagConn = item;
                                        break;
                                    }
                                }
                            }
                        }
                        var r = tagConn as ITagReader;
                        var key = strs[strs.Length - 1];
                        try
                        {
                            var action = r.SetTagReader(key, eval.Eval(v.Value));
                            if (action != null)
                            {
                                action();
                                ValueChangedEventHandler handle = (s1, e1) =>
                                {
                                    ctrl.InvokeAsynchronously(action);
                                };
                                foreach (ITag tag in eval.TagList)
                                {
                                    valueChangedList.Add(new TagNodeHandle(tag.ID, key, tagConn, handle));
                                    tag.ValueChanged += handle;
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            App.AddErrorLog(e);
                            MessageBox.Show(string.Format("设备'{0}'属性'{1}'的值'{2}'转化出错！", string.IsNullOrEmpty(r.Node) ? r.GetType().ToString() : r.Node, key, v.Value));
                        }
                        if (Attribute.IsDefined(tagConn.GetType(), typeof(StartableAttribute), false))
                        {
                            FrameworkElement element = tagConn as FrameworkElement;
                            element.Cursor = Cursors.UpArrow;
                            element.AddHandler(UIElement.MouseEnterEvent, new MouseEventHandler(element_MouseEnter));
                            element.AddHandler(UIElement.MouseLeaveEvent, new MouseEventHandler(element_MouseLeave));
                            element.AddHandler(UIElement.MouseLeftButtonDownEvent, new MouseButtonEventHandler(element_MouseLeftButtonDown));
                        }
                        var hmi = tagConn as HMIControlBase;
                        if (hmi != null && hmi.ShowCaption && !string.IsNullOrEmpty(hmi.Caption))
                        {
                            AdornerLayer lay = AdornerLayer.GetAdornerLayer(hmi);
                            if (lay != null)
                            {
                                TextAdorner frame = new TextAdorner(hmi);
                                frame.Text = hmi.Caption;
                                lay.Add(frame);
                            }
                        }
                    }
                }
            }
            var writer = taglink as ITagWriter;
            if (writer != null && !string.IsNullOrEmpty(writer.TagWriteText))
            {
                var delgts = new List<Delegate>();
                foreach (var item in writer.TagWriteText.GetListFromText())
                {
                    try
                    {
                        if (!string.IsNullOrEmpty(item.Value))
                            delgts.Add(eval.WriteEval(item.Key, item.Value));
                        else
                            delgts.Add(eval.WriteEval(item.Key));
                    }
                    catch (Exception e)
                    {
                        App.AddErrorLog(e);
                        MessageBox.Show(string.Format("设备{0}变量{1}写入PLC公式转换失败", taglink.Node, item.Key) + "\n" + e.Message);
                    }
                    writer.SetTagWriter(delgts);
                }
            }
        }

        static AnimationTimeline animaEnter = new DoubleAnimationUsingKeyFrames
        {
            KeyFrames = new DoubleKeyFrameCollection { new DiscreteDoubleKeyFrame(1.05, KeyTime.FromPercent(0)) }
        };

        static AnimationTimeline animaLeave = new DoubleAnimationUsingKeyFrames
        {
            KeyFrames = new DoubleKeyFrameCollection { new DiscreteDoubleKeyFrame(1, KeyTime.FromPercent(0)) }
        };

        static void element_MouseEnter(object sender, MouseEventArgs e)
        {
            UIElement element = sender as UIElement;
            //ScaleTransform scale = GetScaleTransform(element);
            //scale.BeginAnimation(ScaleTransform.ScaleXProperty, animaEnter);
            //scale.BeginAnimation(ScaleTransform.ScaleYProperty, animaEnter);
            AdornerLayer lay = AdornerLayer.GetAdornerLayer(element);
            if (lay != null)
            {
                FrameAdorner frame = new FrameAdorner(element);
                lay.Add(frame);
            }
        }

        static void element_MouseLeave(object sender, MouseEventArgs e)
        {
            UIElement element = sender as UIElement;
            //var scale = GetScaleTransform(element);
            //scale.BeginAnimation(ScaleTransform.ScaleXProperty, animaLeave);
            //scale.BeginAnimation(ScaleTransform.ScaleYProperty, animaLeave);
            AdornerLayer lay = AdornerLayer.GetAdornerLayer(element);
            if (lay != null)
            {
                var adorners = lay.GetAdorners(element);
                if (adorners != null)
                {
                    FrameAdorner frame = null;
                    for (int i = 0; i < adorners.Length; i++)
                    {
                        frame = adorners[i] as FrameAdorner;
                        if (frame != null)
                        {
                            lay.Remove(frame);
                            return;
                        }
                    }
                }
            }
        }
        static StartDevice frm1 = null;
        static void element_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            HMIControlBase ui = sender as HMIControlBase;
            if (ui != null && !string.IsNullOrEmpty(ui.DeviceName))
            {
                if (frm1 != null)
                    frm1.Close();
                frm1 = new StartDevice(ui.DeviceName, ui.PointToScreen(new Point(ui.ActualWidth / 2, ui.ActualHeight / 2)));
                frm1.Show();
            }
            e.Handled = true;
        }

        public static IEnumerable<DependencyObject> FindVisualChildren(this DependencyObject parent)
        {
            var count = VisualTreeHelper.GetChildrenCount(parent);
            if (count > 0)
            {
                for (var i = 0; i < count; i++)
                {
                    var child = VisualTreeHelper.GetChild(parent, i);
                    yield return child;

                    var children = FindVisualChildren(child);
                    foreach (var item in children)
                        yield return item;
                }
            }
        }

        public static IEnumerable<T> FindChildren<T>(this DependencyObject parent) where T : class
        {
            var count = VisualTreeHelper.GetChildrenCount(parent);
            if (count > 0)
            {
                for (var i = 0; i < count; i++)
                {
                    var child = VisualTreeHelper.GetChild(parent, i);
                    var t = child as T;
                    if (t != null)
                        yield return t;

                    var children = FindChildren<T>(child);
                    foreach (var item in children)
                        yield return item;
                }
            }
        }

        public static IEnumerable<ITagLink> FindTagControls(this DependencyObject parent)
        {
            var count = VisualTreeHelper.GetChildrenCount(parent);
            if (count > 0)
            {
                for (var i = 0; i < count; i++)
                {
                    var child = VisualTreeHelper.GetChild(parent, i);
                    var t = child as ITagLink;
                    if (t != null)
                        yield return t;
                    else
                    {
                        var children = FindTagControls(child);
                        foreach (var item in children)
                            yield return item;
                    }
                }
            }
        }


        public static Window GetParentWindow(this DependencyObject child)
        {
            DependencyObject parentObject = VisualTreeHelper.GetParent(child);

            if (parentObject == null)
            {
                return null;
            }

            Window parent = parentObject as Window;
            if (parent != null)
            {
                return parent;
            }
            else
            {
                return GetParentWindow(parentObject);
            }
        }

        public static void RemoveHandles(this IDataServer srv, params List<TagNodeHandle>[] handleLists)
        {
            foreach (var handleList in handleLists)
            {
                if (handleList != null)
                {
                    foreach (var item in handleList)
                    {
                        srv[item.TagID].ValueChanged -= item.Handle;
                        var element = item.Element as FrameworkElement;
                        if (element != null)
                        {
                            element.RemoveHandler(UIElement.MouseEnterEvent, new MouseEventHandler(element_MouseEnter));
                            element.RemoveHandler(UIElement.MouseLeaveEvent, new MouseEventHandler(element_MouseLeave));
                            element.RemoveHandler(UIElement.MouseLeftButtonDownEvent, new MouseButtonEventHandler(element_MouseLeftButtonDown));
                        }
                    }
                    handleList.Clear();
                }
            }
        }

        public static void SetWindowState(this Window window)
        {
            Dictionary<string, Rect> dict = ConfigCache.Windows;
            string name = window.DependencyObjectType.Name;
            if (dict.ContainsKey(name))
            {
                Rect rec = dict[name];
                if (rec.Width == 0 || rec.Height == 0 || double.IsInfinity(rec.Left) || double.IsInfinity(rec.Top))
                {
                    window.WindowState = WindowState.Maximized;
                    window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                }
                else
                {
                    window.Left = rec.Left;
                    window.Top = rec.Top;
                    window.Width = rec.Width;
                    window.Height = rec.Height;
                }
            }
            else
            {
                ConfigCache.Windows.Add(name, new Rect());
                window.WindowState = WindowState.Maximized;
                window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }
        }

        public static void SaveWindowState(this Window window)
        {
            Dictionary<string, Rect> dict = ConfigCache.Windows;
            dict[window.DependencyObjectType.Name] = window.WindowState == WindowState.Maximized ? new Rect() :
                new Rect(window.Left, window.Top, window.Width, window.Height);
        }


        public static void InvokeAsynchronously(this UIElement element, Action action)
        {
            if (element.Dispatcher.CheckAccess())
            {
                action();
            }
            else
                element.Dispatcher.BeginInvoke(action, System.Windows.Threading.DispatcherPriority.Normal);
        }

    }

    public class TagNodeHandle : IComparable<TagNodeHandle>
    {
        short _tagID;
        public short TagID
        {
            get
            {
                return _tagID;
            }
        }

        string _key;
        public string Key
        {
            get
            {
                return _key;
            }
        }

        ITagLink _element;
        public ITagLink Element
        {
            get
            {
                return _element;
            }
        }

        ValueChangedEventHandler _handle;
        public ValueChangedEventHandler Handle
        {
            get
            {
                return _handle;
            }
        }

        public TagNodeHandle(short tag, string key, ITagLink element, ValueChangedEventHandler handle)
        {
            _tagID = tag;
            _key = key;
            _element = element;
            _handle = handle;
        }

        public int CompareTo(TagNodeHandle other)
        {
            int comp = _tagID.CompareTo(other._tagID);
            return comp == 0 ? _key.CompareTo(other._key) : comp;
        }
    }

}
