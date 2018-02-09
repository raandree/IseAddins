using Microsoft.PowerShell.Host.ISE;
using System.ComponentModel;
using System.Management.Automation;
using System.Windows;
using System.Windows.Controls;
using System.Linq;
using System;
using System.Windows.Threading;
using System.IO;
using System.Windows.Data;
using System.Globalization;
using System.Reflection;
using Microsoft.Windows.PowerShell.Gui.Internal;

namespace IseAddons
{
    /// <summary>
    /// Interaction logic for UserControl1.xaml
    /// </summary>
    public partial class FunctionExplorer : UserControl, IAddOnToolHostObject
    {
        FunctionContainer functions = new FunctionContainer();
        ListXmlStore<string> files = new ListXmlStore<string>();
        ListXmlStore<BreakPoint> breakPoints = new ListXmlStore<BreakPoint>();
        AppFileManager fileManager = new AppFileManager();
        DispatcherTimer timer;
        DispatcherTimer updateTimer;

        StreamWriter sw = null;

        string functionsFileName = "functions.xml";
        string openFilesFileName = "openFiles.xml";
        string breakPointsFileName = "breakPoints.xml";
        string debugLogfileName = "log.txt";

        System.Windows.Controls.Primitives.StatusBarItem statusBarMessage;
        System.Windows.Controls.Primitives.StatusBarItem statusBarContent;

        bool functionsImported = false;
        bool filesOpened = false;
        bool breakPointsImported = false;

        ObjectModelRoot hostObject;

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

                if (!filesOpened)
                {
                    OpenFiles(fileManager.Get(openFilesFileName).FullName);
                    filesOpened = true;

                    UpdateStatusBarMessage(string.Format("Opened {0} files from previous session", hostObject.CurrentPowerShellTab.Files.Count - 1));
                }
            }
        }

        private void ImportBreakpoints(string path)
        {
            if (!System.IO.File.Exists(path))
            {
                return;
            }

            breakPoints = ListXmlStore<BreakPoint>.Import(path);

            if (breakPoints.Count == 0)
            {
                return;
            }

            var cmd = new System.Text.StringBuilder();
            foreach (var breakPoint in breakPoints)
            {
                if (breakPoint.Enabled)
                {
                    cmd.AppendLine(string.Format("Set-PSBreakpoint -Line {0} -Script {1};", breakPoint.LineNumber, breakPoint.ScriptFullName));
                }
                else
                {
                    cmd.AppendLine(string.Format("Set-PSBreakpoint -Line {0} -Script {1} | Disable-PSBreakpoint;", breakPoint.LineNumber, breakPoint.ScriptFullName));
                }
            }
            hostObject.CurrentPowerShellTab.Invoke(cmd.ToString());
        }

        public void OpenFiles(string path)
        {
            if (!System.IO.File.Exists(path))
            {
                return;
            }

            files = ListXmlStore<string>.Import(path);

            foreach (var file in files)
            {
                try
                {
                    hostObject.CurrentPowerShellTab.Files.Add(file);
                }
                catch { }
            }
        }

        public FunctionExplorer()
        {
            InitializeComponent();

            var iseWindow = Application.Current.MainWindow;
            FieldInfo tabControlField = iseWindow.GetType().GetField("runspaceTabControl", BindingFlags.Instance | BindingFlags.NonPublic);
            RunspaceTabControl tabControl = (RunspaceTabControl)tabControlField.GetValue(iseWindow);
            PowerShellTabCollection tabCollection = tabControl.ItemsSource as PowerShellTabCollection;
            ISEFile file = tabCollection.SelectedPowerShellTab.Files.SelectedFile;
            ISEEditor editor = file.Editor;
            
            var mainMenuField = iseWindow.GetType().GetField("mainMenu", BindingFlags.Instance | BindingFlags.NonPublic);
            var mainMenu = (Menu)mainMenuField.GetValue(iseWindow);

            var newItem = new MenuItem();
            newItem.Header = "Open Solution";
           
            //((MenuItem)mainMenu.Items[0]).Items.Add(newItem);
            ((MenuItem)mainMenu.Items[0]).Items.Insert(2, newItem);

            
            var x = hostObject;
            
            

            fileManager.Add(functionsFileName);
            fileManager.Add(openFilesFileName);
            fileManager.Add(breakPointsFileName);
            fileManager.Add(debugLogfileName);

            AppDomain.CurrentDomain.DomainUnload += CurrentDomain_ProcessExit;
            AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit;
            AppDomain.CurrentDomain.FirstChanceException += CurrentDomain_FirstChanceException;

            try
            {
                sw = new System.IO.StreamWriter(fileManager.Get(debugLogfileName).FullName, true);
            }
            catch { }

            statusBarMessage = (System.Windows.Controls.Primitives.StatusBarItem)this.stbBottom.Items[1];
            statusBarContent = (System.Windows.Controls.Primitives.StatusBarItem)this.stbBottom.Items[0];

            timer = new DispatcherTimer();
            timer.Tick += new EventHandler(timer_Tick);

            updateTimer = new DispatcherTimer();
            updateTimer.Tick += new EventHandler(updateTimer_Tick);
            txtUpdateInterval_LostFocus(null, null); //start the timer with the value set in the form
        }

        void CurrentDomain_FirstChanceException(object sender, System.Runtime.ExceptionServices.FirstChanceExceptionEventArgs e)
        {
            //try
            //{
            //    if (sw.BaseStream.CanWrite)
            //    {
            //        sw.WriteLine(DateTime.Now);
            //        sw.WriteLine(e.Exception.Message);
            //        sw.WriteLine(e.Exception.StackTrace);

            //        sw.WriteLine("");
            //        sw.WriteLine("-----------------------------------------------------------------------------------------");
            //        sw.WriteLine("");
            //    }
            //}
            //catch (Exception ex)
            //{
            //    //throw ex;
            //}
        }

        void CurrentDomain_ProcessExit(object sender, EventArgs e)
        {
            try
            {
                UpdateTreeView(true);

                var path = fileManager.Get(functionsFileName).FullName;
                functions.Export(path);
            }
            catch { }

            files.Export(fileManager.Get(openFilesFileName).FullName);
            breakPoints.Export(fileManager.Get(breakPointsFileName).FullName);
        }

        private void CurrentPowerShellTab_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if ((e.PropertyName == "CanInvoke" | e.PropertyName == "LastEditorWithFocus") && this.hostObject.CurrentPowerShellTab.CanInvoke)
            {
                if (!breakPointsImported)
                {
                    ImportBreakpoints(fileManager.Get(breakPointsFileName).FullName);
                    breakPointsImported = true;
                }

                if (chkAutoUpdate.IsChecked.Value)
                {
                    try
                    {
                        UpdateTreeView(false);
                    }
                    catch { }
                }
            }
        }

        private void btnUpdate_Click(object sender, RoutedEventArgs e)
        {
            UpdateTreeView(true);
        }

        private void trvFunctions_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            FunctionDefinition tag;

            var item = (TreeViewItem)((TreeView)sender).SelectedItem;

            if (item == null)
            {
                return;
            }

            tag = item.Tag as FunctionDefinition;

            if (tag == null)
            {
                try
                {
                    var f = hostObject.CurrentPowerShellTab.Files.Where(file => file.DisplayName == item.Header.ToString()).FirstOrDefault();
                    hostObject.CurrentPowerShellTab.Files.SetSelectedFile(f);
                }
                catch { }
            }
            else
            {
                try
                {
                    var f = hostObject.CurrentPowerShellTab.Files.Where(file => file.DisplayName == ((TreeViewItem)item.Parent).Header.ToString()).FirstOrDefault();
                    hostObject.CurrentPowerShellTab.Files.SetSelectedFile(f);

                    hostObject.CurrentPowerShellTab.Files.SelectedFile.Editor.SetCaretPosition(tag.LineNumber, 1);
                }
                catch { }
            }
        }

        private void UpdateTreeView(bool updateBreakpoints)
        {
            try
            {
                //first we try to access the list of files in the editor. If this does not work, the editor is gone. This is important when closing the editor
                var filesCount = hostObject.CurrentPowerShellTab.Files;

                //if the files count could be read we clean the treeview and the files list
                trvFunctions.Dispatcher.Invoke(new Action(() => { trvFunctions.Items.Clear(); }));
                files.Clear();

                //and get a new list from the editor
                files.AddRange(hostObject.CurrentPowerShellTab.Files.Where(file => !file.IsUntitled).Select(file => file.FullPath).ToList());
            }
            catch { }
            

            if (updateBreakpoints && hostObject.CurrentPowerShellTab.CanInvoke)
            {
                breakPoints.Clear();

                breakPoints.AddRange(hostObject.CurrentPowerShellTab.InvokeSynchronous("Get-PSBreakPoint").
                    Select(pso => pso.BaseObject).Cast<LineBreakpoint>().
                    ForEach<LineBreakpoint, BreakPoint>(b => new BreakPoint()
                {
                    ScriptFullName = b.Script,
                    LineNumber = b.Line,
                    Enabled = b.Enabled
                }));
            }

            foreach (var file in hostObject.CurrentPowerShellTab.Files)
            {
                var functionFound = false;
                functions.RemoveFunctionByFile(file.FullPath);

                var scriptItem = new ScriptTreeViewItem(file.FullPath);
                scriptItem.Header = file.DisplayName;
                if (file == hostObject.CurrentPowerShellTab.Files.SelectedFile)
                {
                    scriptItem.IsExpanded = true;
                }

                System.Collections.ObjectModel.Collection<PSParseError> errors = new System.Collections.ObjectModel.Collection<PSParseError>();
                var tokens = PSParser.Tokenize(file.Editor.Text, out errors).Where(t => t.Type == PSTokenType.Keyword | t.Type == PSTokenType.CommandArgument);

                foreach (var token in tokens)
                {
                    if ((token.Content.ToLower() == "function" | token.Content.ToLower() == "workflow"))
                    {
                        functionFound = true;
                        continue;
                    }

                    if (functionFound && token.Type == PSTokenType.CommandArgument)
                    {
                        FunctionDefinition function = new FunctionDefinition(file.FullPath, token.Content, token.StartLine);
                        functions.Add(function);

                        functionFound = false;
                    }
                }
                foreach (var function in (functions.GetFunctionsByFile(file.FullPath).OrderBy(f => f.Name)))
                {
                    var functionItem = new TreeViewItem();
                    functionItem.Header = function.Name;
                    functionItem.Tag = function;

                    scriptItem.Items.Add(functionItem);
                }
                trvFunctions.Dispatcher.Invoke(new Action(() => { trvFunctions.Items.Add(scriptItem); }));
            }

            UpdateStatusBarContent(string.Format("Loaded functions: {0}", functions.Count));

            if (functionsImported)
            {
                try
                {
                    var path = fileManager.Get(functionsFileName).FullName;
                    functions.Export(path);
                }
                catch { }
            }
        }

        public bool GoToDefinition()
        {
            try
            {
                var editor = this.hostObject.CurrentPowerShellTab.Files.SelectedFile.Editor;

                var currentLine = editor.CaretLineText;

                System.Collections.ObjectModel.Collection<PSParseError> errors = new System.Collections.ObjectModel.Collection<PSParseError>();
                var tokens = PSParser.Tokenize(currentLine, out errors).Where(t => t.Type == PSTokenType.Command && t.StartColumn < editor.CaretColumn && t.EndColumn > editor.CaretColumn).ToList();

                var function = functions.GetFunctionByName(tokens[0].Content);

                if (function == null)
                {
                    UpdateStatusBarMessage(string.Format("Function '{0}' not found", tokens[0].Content));
                    return false;
                }

                var iseFile = this.hostObject.CurrentPowerShellTab.Files.Where(file => file.FullPath == function.FullPath).FirstOrDefault();
                if (iseFile != null)
                {
                    this.hostObject.CurrentPowerShellTab.Files.SetSelectedFile(iseFile);
                }
                else
                {
                    try
                    {
                        this.hostObject.CurrentPowerShellTab.Files.Add(function.FullPath);
                        UpdateStatusBarMessage(string.Format("Opened file '{0}'", System.IO.Path.GetFileName(function.FullPath)));
                    }
                    catch
                    {
                        return false;
                    }
                }

                this.hostObject.CurrentPowerShellTab.Files.SelectedFile.Editor.SetCaretPosition(function.LineNumber, 1);

                return true;
            }
            catch
            {
                return false;
            }
        }

        private void UserControl_Loaded_1(object sender, RoutedEventArgs e)
        {
            try
            {
                var path = fileManager.Get(functionsFileName).FullName;
                functions = FunctionContainer.Import((path), "FullName");
            }
            catch
            {
                functions = new FunctionContainer();
            }

            functionsImported = true;
        }

        private void btnClearAndUpdate_Click(object sender, RoutedEventArgs e)
        {
            fileManager.Delete(functionsFileName);

            functions.Clear();

            UpdateTreeView(true);
        }

        private void timer_Tick(object sender, EventArgs e)
        {
            statusBarMessage.Dispatcher.Invoke(new Action(() => { statusBarMessage.Content = ""; }));
        }

        private void updateTimer_Tick(object sender, EventArgs e)
        {
            UpdateTreeView(false);
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

        private void btnClear_Click(object sender, RoutedEventArgs e)
        {
            this.trvFunctions.Items.Clear();
            functions.Clear();

            UpdateStatusBarMessage("Functions cleared");
            var statusBarContent = (System.Windows.Controls.Primitives.StatusBarItem)this.stbBottom.Items[0];
            UpdateStatusBarContent(string.Format("Loaded functions: {0}", functions.Count));
        }

        private void txtUpdateInterval_LostFocus(object sender, RoutedEventArgs e)
        {
            int interval = 0;
            int.TryParse(txtUpdateInterval.Text, out interval);

            if (interval == 0)
            {
                updateTimer.Stop();
            }
            else
            {
                this.updateTimer.Interval = new TimeSpan(0, 0, interval);
                updateTimer.Start();
            }

        }
    }

    public class ScriptTreeViewItem : TreeViewItem
    {
        private string scriptFullName;

        public string ScriptName
        {
            get { return System.IO.Path.GetFileName(scriptFullName); }
        }

        public string ScriptFullName
        {
            get { return scriptFullName; }
        }

        public ScriptTreeViewItem(string scriptFullName)
        {
            this.scriptFullName = scriptFullName;
        }
    }

    [ValueConversion(typeof(bool), typeof(bool))]
    public class NegateBoolenValueConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return !(bool)value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return !(bool)value;
        }
    }
}