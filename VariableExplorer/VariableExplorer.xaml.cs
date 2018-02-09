using Microsoft.PowerShell.Host.ISE;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Management.Automation;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Collections.ObjectModel;
using System.Collections;
using System.Diagnostics;

namespace IseAddons
{
    public partial class VariableExplorer : UserControl, IAddOnToolHostObject
    {
        ObjectModelRoot hostObject;
        System.Windows.Controls.Primitives.StatusBarItem statusBarMessage;
        System.Windows.Controls.Primitives.StatusBarItem statusBarContent;
        DispatcherTimer timer;
        ObservableCollection<NameValuePair> variables;

        private bool firstUpdate = true;

        public VariableExplorer()
        {
            InitializeComponent();

            variables = new ObservableCollection<NameValuePair>();

            statusBarMessage = (System.Windows.Controls.Primitives.StatusBarItem)this.stbBottom.Items[1];
            statusBarContent = (System.Windows.Controls.Primitives.StatusBarItem)this.stbBottom.Items[0];

            timer = new DispatcherTimer();
            timer.Tick += new EventHandler(timer_Tick);
        }

        void CurrentDomain_FirstChanceException(object sender, System.Runtime.ExceptionServices.FirstChanceExceptionEventArgs e)
        {
            Console.WriteLine(e.Exception.Message);
        }

        public ObjectModelRoot HostObject
        {
            get
            {
                return this.hostObject;
            }
            set
            {
                this.hostObject = value;
                this.hostObject.CurrentPowerShellTab.PropertyChanged += new PropertyChangedEventHandler(CurrentPowerShellTab_PropertyChanged);
            }
        }

        private void CurrentPowerShellTab_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if ((e.PropertyName == "CanInvoke" | e.PropertyName == "LastEditorWithFocus") && this.hostObject.CurrentPowerShellTab.CanInvoke)
            {
                if (chkAutoUpdate.IsChecked.Value)
                {
                    try
                    {
                        UpdateTreeView();
                    }
                    catch { }
                }
            }
        }

        private void timer_Tick(object sender, EventArgs e)
        {
            statusBarMessage.Dispatcher.Invoke(new Action(() => { statusBarMessage.Content = ""; }));
        }

        public void UpdateStatusBarMessage(string message)
        {
            statusBarMessage.Dispatcher.Invoke(new Action(() => { statusBarMessage.Content = message; }));
            timer.Interval = new TimeSpan(0, 0, 3);
            timer.Start();
        }

        public void UpdateStatusBarContent(string message)
        {
            statusBarContent.Dispatcher.Invoke(new Action(() => { statusBarContent.Content = message; }));
        }

        public void UpdateTreeView()
        {
            var sw = new Stopwatch();
            sw.Start();

            Collection<PSObject> psVariables = new Collection<PSObject>();

            try
            {
                if (firstUpdate)
                {
                    firstUpdate = false;
                    
                    var systemVars = hostObject.CurrentPowerShellTab.InvokeSynchronous("Get-Variable");
                    var psVariable = new PSObject(new PSVariable("SystemVariables", systemVars));
                    psVariables.Add(psVariable);
                }
                else
                {
                    psVariables = hostObject.CurrentPowerShellTab.InvokeSynchronous("Get-Variable");
                }
            }
            catch
            {
                UpdateStatusBarMessage("Could not get variables from ISE");
            }

            var variables = psVariables.Select(v => v.BaseObject).Cast<PSVariable>().ForEach<PSVariable, Variable>(psv => (Variable)psv);

            trvVariables.Dispatcher.Invoke(new Action(() => { trvVariables.DataContext = variables; }));
            
            UpdateStatusBarContent(string.Format("Loaded items: {0}", trvVariables.Items.Count));

            sw.Stop();

            if (sw.Elapsed > new TimeSpan(0, 0, 0, 0, 500))
            {
                if (chkAutoUpdate.IsChecked.Value)
                    UpdateStatusBarMessage("Update took quite long, disable Auto Update");
            }
        }

        private void trvVariables_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            var item = trvVariables.SelectedItem as NameValuePair;
            item.ReadProperties();
        }

        private void trvVariables_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var item = trvVariables.SelectedItem as NameValuePair;
            item.ReadProperties();
        }

        private void btnUpdate_Click(object sender, RoutedEventArgs e)
        {
            UpdateTreeView();
        }
    }

    public class Variable : NameValuePair
    {
        public Variable()
        { }

        public Variable(string name, object value)
        {
            Name = name;
            Value = value;
        }

        public static implicit operator Variable(PSVariable psVariable)
        {
            return new Variable(psVariable.Name, psVariable.Value);
        }
    }

    public class Property : NameValuePair
    {
        public Property()
        { }

        public Property(string name, object value)
        {
            Name = name;
            Value = value;
        }
    }

    public abstract class NameValuePair
    {
        public string Name { get; set; }
        public object Value { get; set; }

        public ObservableCollection<NameValuePair> Properties { get; set; }

        public NameValuePair()
        {
            Properties = new ObservableCollection<NameValuePair>();
        }

        public void ReadProperties()
        {
            if (this.Value == null) { return; }

            Properties.Clear();

            if (isCollection(this.Value))
            {
                var i = 0;
                foreach (var element in (IEnumerable)this.Value)
                {
                    var elementItem = new Property(string.Format("[{0}]", i), element);
                    this.Properties.Add(elementItem);
                    i++;
                }
            }
            else if (this.Value.GetType().IsArray)
            {
                var i = 0;
                foreach (var element in (IEnumerable)this.Value)
                {
                    var elementItem = new Property(string.Format("[{0}]", i), element);
                    this.Properties.Add(elementItem);
                    i++;
                }
            }

            PropertyInfo[] props;
            if (Value is PSObject)
            {
                props = ((PSObject)Value).BaseObject.GetType().GetProperties();
                Value = ((PSObject)Value).BaseObject;
            }
            else
            {
                props = Value.GetType().GetProperties();
            }

            foreach (var prop in props)
            {
                try
                {
                    var propValue = prop.GetValue(Value, null);
                    var propItem = new Property(prop.Name, propValue);

                    Properties.Add(propItem);
                }
                catch
                {
                    var propItem = new Property(prop.Name, null);

                    Properties.Add(propItem);
                }
            }
        }

        public bool isCollection(object o)
        {
            return typeof(ICollection).IsAssignableFrom(o.GetType())
                || typeof(ICollection<>).IsAssignableFrom(o.GetType());
        }
    }
}