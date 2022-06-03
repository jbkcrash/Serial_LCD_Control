using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Ports;
using System.Text;
//using Newtonsoft.Json;
//using Rectangle = System.Drawing.Rectangle;
//using Brush = System.Drawing.Brush;
using System.Text.Json;
using System.Threading;
using System.Windows;
using System.Windows.Forms;
using Brush = System.Drawing.Brush;
//using System.Collections;
using Brushes = System.Drawing.Brushes;

namespace Serial_LCD_Control
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// This application controls the Turing Smart Screen.
    /// It is based on the Python Code from... TODO Attribute...
    /// </summary>
    public partial class MainWindow : Window
    {
        //Configuration Items
        private bool m_bLoadOnStart;

        private string m_strBackGround;
        private string m_strComboBoxSerial;

        private bool m_bDateTime;
        private bool m_bCPUPercent;
        private bool m_bLogicalCores;
        private bool m_bStartMinimized;

        const string m_strConfigFileName = ".\\SerialLCDConfiguration.json";

        private NotifyIcon m_notifyIcon = new NotifyIcon();
        private WindowState m_storedWindowState = WindowState.Normal;

        int m_iBrightness = 0;
        private readonly BackgroundWorker m_SerialWorker = new BackgroundWorker();

        public MainWindow()
        {
            InitializeComponent();

            m_SerialWorker.WorkerSupportsCancellation = true;
            m_SerialWorker.DoWork += SerialWorkerWork;

            //m_notifyIcon.BalloonTipText = "The app has been minimised. Click the tray icon to show.";
            //m_notifyIcon.BalloonTipTitle = "Serial LCD Control";
            m_notifyIcon.Text = "Serial LCD Control";
            m_notifyIcon.Icon = Resource1.systray;//Icon(@"../../systray.ico");
            m_notifyIcon.Click += new System.EventHandler(m_notifyIcon_Click);
            m_notifyIcon.Visible = true;
        }

        //FrameworkElement.Loaded 
        void OnStateChanged(object sender, EventArgs args)
        {
            if (WindowState == WindowState.Minimized)
            {
                Hide();
                //if (m_notifyIcon != null)
                //    m_notifyIcon.ShowBalloonTip(2000);
            }
            else
                m_storedWindowState = WindowState;
        }

        void m_notifyIcon_Click(object sender, EventArgs e)
        {
            Show();
            WindowState = m_storedWindowState;
        }
        private void OnLoad(object sender, RoutedEventArgs e)
        {
            LoadConfig();
            if (m_bStartMinimized) WindowState = WindowState.Minimized;

            if (m_bLoadOnStart)
            {
                StartLCD();
                buttonStart.Content = "Stop";
            }
            
        }

        void OnClose(object sender, CancelEventArgs e)
        {
            m_notifyIcon.Dispose();
            m_notifyIcon = null;

            m_SerialWorker.CancelAsync();
            Thread.Sleep(1000); //Wait for last drawing and cancel to happen
        }
        private Brush getLoadColor(int iLoad)
        {
            Brush currentBrush = Brushes.Black;
            if (iLoad <= 33) currentBrush = Brushes.Green;
            if (iLoad >= 34) currentBrush = Brushes.Yellow;
            if (iLoad >= 75) currentBrush = Brushes.Red;

            return currentBrush;
        }
        void CheckTrayIcon()
        {
            ShowTrayIcon(!IsVisible);
        }

        void ShowTrayIcon(bool show)
        {
            if (m_notifyIcon != null)
                m_notifyIcon.Visible = show;
        }
        void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs args)
        {
            CheckTrayIcon();
        }
        private void SerialWorkerWork(object sender, DoWorkEventArgs e)
        {
            int iLoad;
            int iLoop;
            int iProcCount = Environment.ProcessorCount;

            PerformanceCounter[] pc;
            PerformanceCounter procTimeCounter;

            Brush currentBrush = Brushes.Green;


            Serial_LCD_Library serial_LCD_Library = new Serial_LCD_Library();

            //if (m_bCPUPercent)
            //{
            procTimeCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            //}
            //if (m_bLogicalCores)
            //{
            pc = new PerformanceCounter[iProcCount]; //Create twelve cores //TODO Set or probe number of cores.
            for (int i = 0; i < iProcCount; i++) pc[i] = new PerformanceCounter("Processor", "% Processor Time", i.ToString());
            //}
            Exception ex = serial_LCD_Library.OpenLCD(m_strComboBoxSerial);
            if (ex  != null) //There was an exception opening the COM port, lets exit the background thread
            {
                System.Windows.MessageBox.Show("A handled exception just occurred: " + ex.Message, "Exception Sample", MessageBoxButton.OK, MessageBoxImage.Warning);
                e.Cancel = true;
                return;
            }


            serial_LCD_Library.Clear(); // We clear the screen

            serial_LCD_Library.SetBrightness(m_iBrightness);

            serial_LCD_Library.SetBackground(m_strBackGround);

            DateTime dtNow = DateTime.Now;
            DateTime dtBefore = DateTime.Now.AddSeconds(-1);

            while (m_SerialWorker.CancellationPending != true) //Enter into inner loop until we are cancelling
            {
                Thread.Sleep(10);
                dtNow = DateTime.Now;
                if ((dtBefore.AddSeconds(1) <= dtNow))
                {
                    dtBefore = dtNow;
                    if (m_bCPUPercent)
                    {
                        int iTotalLoad = Convert.ToInt32(procTimeCounter.NextValue());
                        serial_LCD_Library.DisplayText(String.Format("{0}%", iTotalLoad), getLoadColor(iTotalLoad), Brushes.White, 180, 20,
                            true,
                            "Times New Roman", 40, 10);
                    }

                    if (m_bDateTime) //Should only update greater than a second.
                    {
                        
                        serial_LCD_Library.DisplayText(dtNow.ToString("dddd, dd MMMM yy hh:mm.ss tt"),
                            Brushes.Black, Brushes.White, 0, 420);
                    }
                }

                if (m_bLogicalCores)
                {
                    iLoop = 0;
                    //int iAvgLoad = 0;
                    //int[] iPreviousLoads = new int[pc.Length];
                    //TODO maybe we can optimize the display speed by drawing these all at same time?
                    //TODO align with number of cores instead hardcoded location on screen
                    foreach (PerformanceCounter pccurrent in pc)
                    {
                        iLoad = (int)Math.Round(pccurrent.NextValue());
                        //if (iLoad > iPreviousLoads[iLoop])
                        //    iAvgLoad = (iLoad - iPreviousLoads[iLoop]) / 2;
                        //else
                        //    iAvgLoad = (iPreviousLoads[iLoop] - iLoad) / 2;
                        
                        //iPreviousLoads[iLoop] = iLoad;
                        serial_LCD_Library.DisplayProgressBar(5, 300 + (iLoop * 10), 310, 9, getLoadColor(iLoad), Brushes.White, 0, 100, iLoad, false, true);
                        iLoop++;
                    }
                }
            }
            serial_LCD_Library.Clear();
            serial_LCD_Library.CloseLCD();
            e.Cancel = true;
            Console.WriteLine("Exiting Background Worker");
            return;
        }
        private void OnConfigChange(object sender, RoutedEventArgs e)
        {
            buttonSave.IsEnabled = true;
        }
        private void StartLCD()
        {
            m_SerialWorker.RunWorkerAsync();
        }
        private void Start(object sender, RoutedEventArgs e)
        {
            if (!m_SerialWorker.IsBusy)
            {
                StartLCD();
                buttonStart.Content = "Stop";
            }
            else
            {
                buttonStart.IsEnabled = false;
                m_SerialWorker.CancelAsync();
                Thread.Sleep(2000); //Wait for last drawing and cancel to happen
                buttonStart.Content = "Start";
                buttonStart.IsEnabled = true;
            }
        }


        private void restoreWindow(object sender, EventArgs e)
        {
            Activate();
            this.WindowState = WindowState.Normal;
            m_notifyIcon.Visible = false;
        }

        private void backGroundImagePath_TextChanged(object sender, RoutedEventArgs e)
        {
            //configure save file dialog box
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();
            dlg.FileName = "background"; //default file name
            dlg.DefaultExt = ".png"; //default file extension
            dlg.Filter = "Image documents (.png)|*.png"; //filter files by extension

            // Show save file dialog box
            Nullable<bool> result = dlg.ShowDialog();

            // Process save file dialog box results
            if (result == true)
            {
                // Save document
                string filename = dlg.FileName;
                backGroundImagePath.Text = filename;
            }
        }
        private void LoadConfig()
        {
            string jsonString;

            //Load our Combobox with the available COM ports
            string[] ports = SerialPort.GetPortNames();
            foreach (string port in ports) comboBoxCOM.Items.Add(port);

            if (File.Exists(m_strConfigFileName))
            {
                jsonString = File.ReadAllText(m_strConfigFileName, Encoding.UTF8);
            }
            else
            {
                //Create default file...
                Configuration confDefault = new Configuration();
                jsonString = JsonSerializer.Serialize(confDefault);
                File.WriteAllText(m_strConfigFileName, jsonString);
            }

            Configuration configuration = JsonSerializer.Deserialize<Configuration>(jsonString);

            comboBoxCOM.Text = m_strComboBoxSerial = configuration.strCOMPort;
            
            m_bLoadOnStart = (checkBoxStart.IsChecked = configuration.bStartOnLoad) ?? false;
            m_bStartMinimized = (checkBoxStartMinimized.IsChecked = configuration.bStartMinimized) ?? false;

            m_strBackGround = backGroundImagePath.Text = configuration.strBackground;
            if (m_strBackGround == "DefaultImage")
            {
                checkBoxDefaultImage.IsChecked = true;
            }

            m_iBrightness = Convert.ToInt32(Brightness.Value = configuration.Brightness);
            m_bDateTime = (checkBoxDateTime.IsChecked = configuration.bDateTime) ?? false;
            m_bCPUPercent = (checkBoxCPUPercent.IsChecked = configuration.bCPUPercent) ?? false;
            m_bLogicalCores = (checkBoxLogicalCores.IsChecked = configuration.bLogicalCores) ?? false;

            buttonSave.IsEnabled = false;

        }
        private void SaveConfig(object sender, RoutedEventArgs e)
        {
            Configuration configuration = new Configuration();
            configuration.strCOMPort = comboBoxCOM.Text;
            m_bLoadOnStart = configuration.bStartOnLoad = checkBoxStart.IsChecked ?? false;
            m_bStartMinimized = configuration.bStartMinimized = checkBoxStartMinimized.IsChecked ?? false;

            //bDefaultImage = checkBoxDefaultImage.IsChecked ?? false;
            if (checkBoxDefaultImage.IsChecked ?? false == true)
                backGroundImagePath.Text = m_strBackGround = configuration.strBackground = "DefaultImage";
            else
                m_strBackGround = configuration.strBackground = backGroundImagePath.Text;

            m_iBrightness = Convert.ToInt32(configuration.Brightness = Brightness.Value);
            m_bDateTime = configuration.bDateTime = checkBoxDateTime.IsChecked ?? false;
            m_bCPUPercent = configuration.bCPUPercent = checkBoxCPUPercent.IsChecked ?? false;
            m_bLogicalCores = configuration.bLogicalCores = checkBoxLogicalCores.IsChecked ?? false;

            string jsonString = JsonSerializer.Serialize(configuration);
            File.WriteAllText(m_strConfigFileName, jsonString);

            buttonSave.IsEnabled = false;
        }

        private void brightnessValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            m_iBrightness = Convert.ToInt32(Brightness.Value);
            buttonSave.IsEnabled = true;
        }

        private void CheckBox_Dirty(object sender, RoutedEventArgs e)
        {
            buttonSave.IsEnabled = true;
        }
    }
}
