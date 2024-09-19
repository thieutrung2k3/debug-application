using debug_application_ver2.entity;
using LiveCharts;
using LiveCharts.Defaults;
using LiveCharts.Definitions.Charts;
using LiveCharts.Wpf;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO.Ports;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace debug_application_ver2
{

    public partial class MainWindow : Window
    {
        double workAreaWidth = SystemParameters.WorkArea.Width;
        double workAreaHeight = SystemParameters.WorkArea.Height;
        private SerialPort _serialPort;
        public ChartValues<double> Values { get; set; }
        public Func<double, string> Formatter { get; set; }
        private DispatcherTimer _timer;
        private Random _random = new Random();
        private int _index = 0;
        public ChartValues<double> ChartValues { get; set; }
        private List<string> _lines = new List<string>();
        private const int MaxLines = 50;
        public MainWindow()
        {
            InitializeComponent();
            InitializeData();
            InitializeChart();
        }
        private void SetButtonBorderBrush()
        {
            // Tạo màu từ ARGB (53, 116, 240)
            System.Windows.Media.Color color = System.Windows.Media.Color.FromArgb(255, 53, 116, 240); // Alpha = 255 cho màu không trong suốt
            SolidColorBrush brush = new SolidColorBrush(color);

            // Áp dụng màu vào BorderBrush của nút
            btnSetting.BorderBrush = brush;
            double width = myGrid.ColumnDefinitions[0].ActualWidth;
            if (width != 0)
            {
                btnSetting.BorderThickness = new Thickness(0, 0, 0, 1);
            }
            else
            {
                btnSetting.BorderThickness = new Thickness(0, 0, 0, 0);
            }

        }
        private void InitializeChart()
        {
            PrepareChart(chart1);
            PrepareChart(chart2);
            PrepareChart(chart3);
            PrepareChart(chart4);
        }
        private void PrepareChart(CartesianChart chart)
        {
            chart.AxisY.Add(new Axis
            {
                Title = "Value",
                MinValue = 0, // Giá trị tối thiểu của trục Y
                MaxValue = 100, // Giá trị tối đa của trục Y
                LabelFormatter = value => value.ToString("N") // Định dạng nhãn
            });
            chart.Series = new SeriesCollection
            {
                new LineSeries
                {
                    Title = "Sample Data",
                    Values = new ChartValues<double> { 0 }
                }
            };
        }
        private void InitializeData()
        {
            // Đặt kích thước của cửa sổ theo kích thước vùng làm việc

            this.BorderThickness = new Thickness(0);
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            // Import port
            LoadPorts();
            //Import baud rate
            int[] baudRates = { 300, 1200, 2400, 4800, 9600, 14400, 19200, 28800, 38400, 57600, 115200, 230400, 460800, 921600, 1000000, 2000000, 3000000 };
            cbBaudRate.ItemsSource = baudRates;
            
        }

        private void LoadPorts()
        {
            String[] ports = SerialPort.GetPortNames();
            cbPorts.ItemsSource = ports;
        }
        private bool IsWindowMaximized()
        {
            if (this.Width == workAreaWidth && this.Height == workAreaHeight)
            {
                return true;
            }
            return false;
        }
        private DispatcherTimer _dataReadTimer;

        private void Connection()
        {
            try
            {
                // Lấy thông tin cổng và tốc độ baud từ giao diện người dùng
                string port = cbPorts.SelectedItem.ToString();
                int baudRate = int.Parse(cbBaudRate.SelectedItem.ToString());

                // Khởi tạo đối tượng SerialPort
                _serialPort = new SerialPort(port, baudRate);

                // Mở cổng nối tiếp
                _serialPort.Open();

                // Khởi tạo và bắt đầu Timer
                _dataReadTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(5) // Đọc dữ liệu mỗi 5ms
                };
                _dataReadTimer.Tick += async (sender, e) => await DataReadTimer_Tick(sender, e);
                _dataReadTimer.Start();
            }
            catch (UnauthorizedAccessException)
            {
                tbError.Text += DateTime.Now.ToString() + "UnauthorizedAccessException: The COM port is in use by another application.\n";
            }
            catch (Exception ex)
            {
                tbError.Text += DateTime.Now.ToString() + ex.ToString();
            }
        }


        private void Disconnection()
        {
            try
            {
                // Ngừng Timer
                if (_dataReadTimer != null)
                {
                    _dataReadTimer.Stop();
                    _dataReadTimer = null;
                }

                // Kiểm tra xem _serialPort có phải là null không và nếu đã mở thì đóng
                if (_serialPort != null)
                {
                    if (_serialPort.IsOpen)
                    {
                        // Đóng cổng nối tiếp
                        _serialPort.Close();
                    }

                    // Giải phóng tài nguyên
                    _serialPort.Dispose();
                    _serialPort = null;
                }
            }
            catch (UnauthorizedAccessException)
            {
                MessageBox.Show("Không thể ngắt kết nối vì cổng COM đang được sử dụng.");
            }
            catch (Exception ex)
            {
                tbError.Text += DateTime.Now.ToString() + ex.Message + "\n";
            }
        }

        private async Task DataReadTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                if (_serialPort != null && _serialPort.IsOpen)
                {
                    if (_serialPort.BytesToRead > 0)
                    {
                        string inData = _serialPort.ReadLine();
                        await Dispatcher.InvokeAsync(() => UpdateUI(inData));
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi nhận dữ liệu: {ex.Message}");
            }
        }


        private void UpdateUI(string inData)
        {
            _lines.Add(inData);
            if (_lines.Count > 10)
            {
                _lines.RemoveAt(0);
            }
            tbResult.Text = string.Join(Environment.NewLine, _lines);
            tbResult.ScrollToEnd();

            string[] parts = inData.Split(';');
            if (parts.Length > 1)
            {
                string temp = parts[1].Substring(1);
                if (double.TryParse(temp, out double value))
                {
                    // Sử dụng async/await để cập nhật đồ thị
                    _ = UpdateChartAsync(value); // Ignore the returned Task
                }
                else
                {
                    tbError.Text += DateTime.Now.ToString() + "Value cannot be converted to number.\n";
                }
            }
            else
            {
                tbError.Text += DateTime.Now.ToString() + "Data is not in correct format.\n";
            }
        }

        private async Task UpdateChartAsync(double value)
        {
            await Dispatcher.InvokeAsync(() =>
            {
                // Giả sử `SeriesCollection` chứa `LineSeries` bạn muốn cập nhật
                var lineSeries = chart1.Series[0] as LineSeries;
                if (lineSeries != null)
                {
                    if (lineSeries.Values.Count > MaxLines)
                    {
                        lineSeries.Values.RemoveAt(0); // Xóa điểm dữ liệu cũ nhất
                    }
                    lineSeries.Values.Add(value);
                }
            });
        }



        private void window_loaded(object sender, RoutedEventArgs e)
        {
            SetButtonBorderBrush();
            chart1.Height = myGrid.RowDefinitions[0].ActualHeight - 50;
            chart2.Height = myGrid.RowDefinitions[0].ActualHeight - 50;
            chart3.Height = myGrid.RowDefinitions[1].ActualHeight - 50;
            chart4.Height = myGrid.RowDefinitions[1].ActualHeight - 50;
        }

        private void btnSetting_Click(object sender, RoutedEventArgs e)
        {
            double width = totalGrid.ColumnDefinitions[0].ActualWidth;
            Storyboard sb = (Storyboard)this.Resources["CollapseColumnAnimation"];
            Storyboard sbReturn = (Storyboard)this.Resources["CollapseColumnReturnAnimation"];
            
            if (width == 0)
            {
                btnSetting.BorderThickness = new Thickness(0, 0, 0, 1);
                sbReturn.Begin();
            }
            else
            {
                btnSetting.BorderThickness = new Thickness(0, 0, 0, 0);
                sb.Begin();
            }
            
            
        }

        private void btnRun_Click(object sender, RoutedEventArgs e)
        {
            if (_serialPort != null && _serialPort.IsOpen)
            {
                // Nếu cổng nối tiếp đang mở, ngắt kết nối
                Disconnection();

                // Cập nhật trạng thái của nút và icon
                iconRun.Kind = MahApps.Metro.IconPacks.PackIconMaterialKind.Run;
                iconRun.Foreground = new SolidColorBrush(Color.FromArgb(255, 133, 195, 134));
                btnRun.Background = new SolidColorBrush(Color.FromArgb(255, 30, 31, 34));
            }
            else
            {
                // Nếu cổng nối tiếp không mở, kết nối
                Connection();

                if (_serialPort != null && _serialPort.IsOpen)
                {
                    // Nếu kết nối thành công, cập nhật trạng thái của nút và icon
                    iconRun.Kind = MahApps.Metro.IconPacks.PackIconMaterialKind.Meditation;
                    iconRun.Foreground = new SolidColorBrush(Color.FromArgb(255, 255, 255, 255));
                    btnRun.Background = new SolidColorBrush(Color.FromArgb(255, 181, 71, 71));
                }
            }
        }
        
        bool check1 = false;
        bool check2 = false;
        bool check3 = false;
        bool check4 = false;
        private CartesianChart SelectChart(bool z1, bool z2, bool z3, bool z4)
        {
            if (z1)
            {
                if (!check1)
                {
                    zone1.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 181, 71, 71));
                    zone2.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 57, 59, 64));
                    zone3.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 57, 59, 64));
                    zone4.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 57, 59, 64));
                    check2 = false;
                    check3 = false;
                    check4 = false;
                    return chart1;
                }
                else
                {
                    zone1.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 57, 59, 64));
                }
            }
            if (z2)
            {
                if (!check2)
                {
                    zone2.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 181, 71, 71));
                    zone1.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 57, 59, 64));
                    zone3.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 57, 59, 64));
                    zone4.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 57, 59, 64));
                    check1 = false;
                    check3 = false;
                    check4 = false;
                    return chart2;
                }
                else
                {
                    zone2.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 57, 59, 64));
                }
            }
            if (z3)
            {
                if (!check3)
                {
                    zone3.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 181, 71, 71));
                    zone2.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 57, 59, 64));
                    zone1.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 57, 59, 64));
                    zone4.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 57, 59, 64));
                    check2 = false;
                    check1 = false;
                    check4 = false;
                    return chart3;
                }
                else
                {
                    zone3.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 57, 59, 64));
                }
            }
            if (z4)
            {
                if (!check4)
                {
                    zone4.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 181, 71, 71));
                    zone2.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 57, 59, 64));
                    zone3.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 57, 59, 64));
                    zone1.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 57, 59, 64));
                    check2 = false;
                    check3 = false;
                    check1 = false;
                    return chart4;
                }
                else
                {
                    zone4.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 57, 59, 64));
                }
            }
            return new CartesianChart();
        }

        

        private void btnTools_Click(object sender, RoutedEventArgs e)
        {
            popup.IsOpen = true;
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }
        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void cbPorts_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            LoadPorts();
        }

        private void zone1_MouseDown(object sender, MouseButtonEventArgs e)
        {
            check1 = !check1;
            CartesianChart s = SelectChart(true, false, false, false);
            chartName.Content = s.Name;
        }

        private void zone2_MouseDown(object sender, MouseButtonEventArgs e)
        {
            check2 = !check2;
            CartesianChart s = SelectChart(false, true, false, false);
            chartName.Content = s.Name;
        }

        private void zone3_MouseDown(object sender, MouseButtonEventArgs e)
        {
            check3 = !check3;
            CartesianChart s = SelectChart(false, false, true, false);
            chartName.Content = s.Name;
        }
        private void zone4_MouseDown(object sender, MouseButtonEventArgs e)
        {
            check4 = !check4;
            CartesianChart s = SelectChart(false, false, false, true);
            chartName.Content = s.Name;
        }

        private void MySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            //string[] parts = chartName.Content.ToString().Split(':');
            string name = chartName.Content.ToString();
            if (name == chart1.Name)
            {
                tbSetWidth.Text = sldSetWidth.Value.ToString();
                String sWidth = sldSetWidth.Value.ToString();
                int width = int.Parse(sWidth);
                myGrid.ColumnDefinitions[0].Width = new GridLength(width);
            }
            if (name == chart3.Name)
            {
                tbSetWidth.Text = sldSetWidth.Value.ToString();
                String sWidth = sldSetWidth.Value.ToString();
                int width = int.Parse(sWidth);
                myGrid.ColumnDefinitions[0].Width = new GridLength(width);
            }
            if (name == chart2.Name)
            {
                tbSetWidth.Text = sldSetWidth.Value.ToString();
                String sWidth = sldSetWidth.Value.ToString();
                int width = int.Parse(sWidth);
                myGrid.ColumnDefinitions[0].Width = new GridLength(width);
            }
            if (name == chart4.Name)
            {
                tbSetWidth.Text = sldSetWidth.Value.ToString();
                String sWidth = sldSetWidth.Value.ToString();
                int width = int.Parse(sWidth);
                myGrid.ColumnDefinitions[0].Width = new GridLength(width);
            }


        }
    }
}
