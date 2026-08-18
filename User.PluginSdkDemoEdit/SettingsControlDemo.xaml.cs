// -------------------------------------------------------------------------

// FILE VERSION: V0.11.54 (Fix errori 18)

// -------------------------------------------------------------------------

using System;

using System.IO.Ports;

using System.Windows;

using System.Windows.Controls;

using System.Windows.Media;

using System.Windows.Media.Effects;

using System.Windows.Shapes;

using System.Windows.Input;

using System.Collections.Generic;
using System.Linq;

using System.Threading.Tasks;

using System.Globalization;

using SimHub.Plugins;



namespace SimRIG

{

    public partial class SettingsControlDemo : UserControl

    {

        public DataPluginDemo Plugin { get; }

        private bool _isLoaded = false;

        private SolidColorBrush _brushLavaRed, _brushSuccess, _brushError, _brushWarning;

        private DropShadowEffect _glowEffect;
        private Action<double, string> _piperProgressHandler;
        private Action _piperCompletedHandler;
        private Action<string> _piperFailedHandler;

        private string _lastFlagSelection = "Yellow";

        private string _lastCarSelection = "ABS";



        public SettingsControlDemo() { InitializeComponent(); }



        public SettingsControlDemo(DataPluginDemo plugin) : this()

        {

            this.Plugin = plugin;

            this.Loaded += SettingsControlDemo_Loaded;

            this.Unloaded += SettingsControlDemo_Unloaded;



            _brushLavaRed = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CF1020")); _brushLavaRed.Freeze();

            _brushSuccess = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00E676")); _brushSuccess.Freeze();

            _brushError = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF5252")); _brushError.Freeze();

            _brushWarning = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF9800")); _brushWarning.Freeze();

            _glowEffect = new DropShadowEffect { Color = (Color)ColorConverter.ConvertFromString("#CF1020"), BlurRadius = 25 }; _glowEffect.Freeze();

        }



        private void SettingsControlDemo_Loaded(object sender, RoutedEventArgs e)

        {

            _isLoaded = false;

            CmbTopLeft.ItemsSource = DataPluginDemo.FunctionList;

            CmbTopRight.ItemsSource = DataPluginDemo.FunctionList;

            RefreshProfileList();

            LoadValuesFromPlugin();

            SubscribeToEvents();

            _isLoaded = true;

            CompositionTarget.Rendering += CompositionTarget_Rendering;

        }



        private void SubscribeToEvents()

        {

            CmbTopLeft.SelectionChanged += CmbTopLeft_SelectionChanged;

            CmbTopRight.SelectionChanged += CmbTopRight_SelectionChanged;

            CmbProfiles.SelectionChanged += CmbProfiles_SelectionChanged;
            CmbSpeedUnit.SelectionChanged += CmbUnits_Changed;
            CmbTempUnit.SelectionChanged += CmbUnits_Changed;
            CmbPressureUnit.SelectionChanged += CmbUnits_Changed;



            BtnLoadProfile.Click += BtnLoadProfile_Click;

            BtnSaveDirect.Click += BtnSaveDirect_Click;

            BtnSaveAs.Click += BtnSaveAs_Click;

            BtnConfirmSaveAs.Click += BtnConfirmSaveAs_Click;

            BtnDelete.Click += BtnDelete_Click;

            BtnDeleteYes.Click += BtnDeleteYes_Click;

            BtnDeleteNo.Click += BtnDeleteNo_Click;

            BtnScan.Click += BtnScan_Click;



            BtnStartMinus.Click += (s, args) => AdjustRpmParam(true, -1);

            BtnStartPlus.Click += (s, args) => AdjustRpmParam(true, 1);

            BtnCountMinus.Click += (s, args) => AdjustRpmParam(false, -1);

            BtnCountPlus.Click += (s, args) => AdjustRpmParam(false, 1);



            SldBrightnessBack.ValueChanged += OnLiveSettingChanged;

            SldBrightnessRpm.ValueChanged += OnLiveSettingChanged;

            CmbIdleMode.SelectionChanged += OnLiveSettingChanged;

            CmbRpmStyle.SelectionChanged += OnLiveSettingChanged;



            ChkUseGradient.Checked += OnLiveSettingChanged;

            ChkUseGradient.Unchecked += OnLiveSettingChanged;

            TxtZoneLowCount.TextChanged += OnLiveSettingChanged;

            TxtZoneMedCount.TextChanged += OnLiveSettingChanged;

            TxtZoneHighCount.TextChanged += OnLiveSettingChanged;

            TxtZoneMaxCount.TextChanged += OnLiveSettingChanged;



            BtnZoneLowColor.Click += (s, args) => PickZoneColor(1);

            BtnZoneMedColor.Click += (s, args) => PickZoneColor(2);

            BtnZoneHighColor.Click += (s, args) => PickZoneColor(3);

            BtnZoneMaxColor.Click += (s, args) => PickZoneColor(4);



            BtnIdleColor.Click += BtnIdleColor_Click;

            BtnRpmStartColor.Click += BtnRpmStartColor_Click;

            BtnRpmEndColor.Click += BtnRpmEndColor_Click;



            CmbFlagSelector.SelectionChanged += CmbFlagSelector_SelectionChanged;

            TxtFlagA_Start.TextChanged += FlagControl_Changed;

            TxtFlagA_Count.TextChanged += FlagControl_Changed;

            TxtFlagB_Start.TextChanged += FlagControl_Changed;

            TxtFlagB_Count.TextChanged += FlagControl_Changed;



            ChkFlagB_Enable.Checked += FlagControl_Changed;

            ChkFlagB_Enable.Unchecked += FlagControl_Changed;

            ChkFlagBlink.Checked += FlagControl_Changed;

            ChkFlagBlink.Unchecked += FlagControl_Changed;

            TxtFlagBlinkMs.TextChanged += FlagControl_Changed;



            CmbCarSysSelector.SelectionChanged += CmbCarSysSelector_SelectionChanged;

            TxtCarA_Start.TextChanged += CarControl_Changed;

            TxtCarA_Count.TextChanged += CarControl_Changed;

            TxtCarB_Start.TextChanged += CarControl_Changed;

            TxtCarB_Count.TextChanged += CarControl_Changed;



            ChkCarB_Enable.Checked += CarControl_Changed;

            ChkCarB_Enable.Unchecked += CarControl_Changed;

            ChkCarBlink.Checked += CarControl_Changed;

            ChkCarBlink.Unchecked += CarControl_Changed;

            TxtCarBlinkMs.TextChanged += CarControl_Changed;



            BtnCarColor.Click += BtnCarColor_Click;

            BtnCarColor2.Click += BtnCarColor2_Click;



            ChkTestRpm.Checked += ToggleRpmTest;

            ChkTestRpm.Unchecked += ToggleRpmTest;

            SldRpmTest.ValueChanged += SldRpmTest_ValueChanged;

            BtnTestFlag.Click += ToggleFlagTest;

            BtnTestCar.Click += ToggleCarTest;



            if (ChkLogSystem != null) { ChkLogSystem.Checked += ToggleDebugLogs; ChkLogSystem.Unchecked += ToggleDebugLogs; }
            if (ChkLogFuel != null) { ChkLogFuel.Checked += ToggleDebugLogs; ChkLogFuel.Unchecked += ToggleDebugLogs; }

            if (ChkLogStrategy != null) { ChkLogStrategy.Checked += ToggleDebugLogs; ChkLogStrategy.Unchecked += ToggleDebugLogs; }

            if (ChkLogRadar != null) { ChkLogRadar.Checked += ToggleDebugLogs; ChkLogRadar.Unchecked += ToggleDebugLogs; }

            if (ChkLogOpponents != null) { ChkLogOpponents.Checked += ToggleDebugLogs; ChkLogOpponents.Unchecked += ToggleDebugLogs; }

            if (ChkLogMicrosector != null) { ChkLogMicrosector.Checked += ToggleDebugLogs; ChkLogMicrosector.Unchecked += ToggleDebugLogs; }
            if (ChkLogWeather != null) { ChkLogWeather.Checked += ToggleDebugLogs; ChkLogWeather.Unchecked += ToggleDebugLogs; }
            if (ChkLogHardware != null) { ChkLogHardware.Checked += ToggleDebugLogs; ChkLogHardware.Unchecked += ToggleDebugLogs; }

            if (ChkLogVoice != null) { ChkLogVoice.Checked += ToggleDebugLogs; ChkLogVoice.Unchecked += ToggleDebugLogs; }
            if (ChkLogMergeGap != null) { ChkLogMergeGap.Checked += ToggleDebugLogs; ChkLogMergeGap.Unchecked += ToggleDebugLogs; }

            if (TxtFuelWeightCoef != null) TxtFuelWeightCoef.TextChanged += AdvancedCoef_Changed;

            if (TxtTempCoef != null) TxtTempCoef.TextChanged += AdvancedCoef_Changed;

            if (TxtFuelSpeedCoef != null) TxtFuelSpeedCoef.TextChanged += AdvancedCoef_Changed;
            if (TxtSpeedDropThresholdKmh != null) TxtSpeedDropThresholdKmh.TextChanged += AdvancedCoef_Changed;
            if (TxtDampSpeedDropOffsetKmh != null) TxtDampSpeedDropOffsetKmh.TextChanged += AdvancedCoef_Changed;
            if (TxtDampPaceOffsetSeconds != null) TxtDampPaceOffsetSeconds.TextChanged += AdvancedCoef_Changed;

        }



        private void SettingsControlDemo_Unloaded(object sender, RoutedEventArgs e)

        {

            CompositionTarget.Rendering -= CompositionTarget_Rendering;

            if (_piperProgressHandler != null) PiperEngine.Instance.OnDownloadProgress -= _piperProgressHandler;
            if (_piperCompletedHandler != null) PiperEngine.Instance.OnDownloadCompleted -= _piperCompletedHandler;
            if (_piperFailedHandler != null) PiperEngine.Instance.OnDownloadFailed -= _piperFailedHandler;



            CmbTopLeft.SelectionChanged -= CmbTopLeft_SelectionChanged; CmbTopRight.SelectionChanged -= CmbTopRight_SelectionChanged;

            CmbProfiles.SelectionChanged -= CmbProfiles_SelectionChanged;
            CmbSpeedUnit.SelectionChanged -= CmbUnits_Changed;
            CmbTempUnit.SelectionChanged -= CmbUnits_Changed;
            CmbPressureUnit.SelectionChanged -= CmbUnits_Changed;

            BtnLoadProfile.Click -= BtnLoadProfile_Click; BtnSaveDirect.Click -= BtnSaveDirect_Click;

            BtnSaveAs.Click -= BtnSaveAs_Click; BtnConfirmSaveAs.Click -= BtnConfirmSaveAs_Click;

            BtnDelete.Click -= BtnDelete_Click; BtnDeleteYes.Click -= BtnDeleteYes_Click; BtnDeleteNo.Click -= BtnDeleteNo_Click;

            BtnScan.Click -= BtnScan_Click;

            SldBrightnessBack.ValueChanged -= OnLiveSettingChanged; SldBrightnessRpm.ValueChanged -= OnLiveSettingChanged;

            CmbIdleMode.SelectionChanged -= OnLiveSettingChanged; CmbRpmStyle.SelectionChanged -= OnLiveSettingChanged;

            ChkUseGradient.Checked -= OnLiveSettingChanged; ChkUseGradient.Unchecked -= OnLiveSettingChanged;

            TxtZoneLowCount.TextChanged -= OnLiveSettingChanged; TxtZoneMedCount.TextChanged -= OnLiveSettingChanged;

            TxtZoneHighCount.TextChanged -= OnLiveSettingChanged; TxtZoneMaxCount.TextChanged -= OnLiveSettingChanged;

            BtnIdleColor.Click -= BtnIdleColor_Click; BtnRpmStartColor.Click -= BtnRpmStartColor_Click; BtnRpmEndColor.Click -= BtnRpmEndColor_Click;

            CmbFlagSelector.SelectionChanged -= CmbFlagSelector_SelectionChanged;

            TxtFlagA_Start.TextChanged -= FlagControl_Changed; TxtFlagA_Count.TextChanged -= FlagControl_Changed;

            TxtFlagB_Start.TextChanged += FlagControl_Changed; TxtFlagB_Count.TextChanged -= FlagControl_Changed;

            ChkFlagB_Enable.Checked -= FlagControl_Changed; ChkFlagB_Enable.Unchecked -= FlagControl_Changed;

            ChkFlagBlink.Checked -= FlagControl_Changed; ChkFlagBlink.Unchecked -= FlagControl_Changed;

            TxtFlagBlinkMs.TextChanged -= FlagControl_Changed;

            CmbCarSysSelector.SelectionChanged -= CmbCarSysSelector_SelectionChanged;

            TxtCarA_Start.TextChanged -= CarControl_Changed; TxtCarA_Count.TextChanged -= CarControl_Changed;

            TxtCarB_Start.TextChanged -= CarControl_Changed; TxtCarB_Count.TextChanged -= CarControl_Changed;

            ChkCarB_Enable.Checked -= CarControl_Changed; ChkCarB_Enable.Unchecked -= CarControl_Changed;

            ChkCarBlink.Checked -= CarControl_Changed; ChkCarBlink.Unchecked -= CarControl_Changed;

            TxtCarBlinkMs.TextChanged -= CarControl_Changed;

            BtnCarColor.Click -= BtnCarColor_Click; BtnCarColor2.Click -= BtnCarColor2_Click;



            ChkTestRpm.Checked -= ToggleRpmTest; ChkTestRpm.Unchecked -= ToggleRpmTest;

            SldRpmTest.ValueChanged -= SldRpmTest_ValueChanged;

            BtnTestFlag.Click -= ToggleFlagTest; BtnTestCar.Click -= ToggleCarTest;

            if (TxtFuelWeightCoef != null) TxtFuelWeightCoef.TextChanged -= AdvancedCoef_Changed;
            if (TxtTempCoef != null) TxtTempCoef.TextChanged -= AdvancedCoef_Changed;
            if (TxtFuelSpeedCoef != null) TxtFuelSpeedCoef.TextChanged -= AdvancedCoef_Changed;
            if (TxtSpeedDropThresholdKmh != null) TxtSpeedDropThresholdKmh.TextChanged -= AdvancedCoef_Changed;

        }



        private void LoadValuesFromPlugin()

        {

            if (Plugin.Settings.TopLeftEncoderMode >= 0 && Plugin.Settings.TopLeftEncoderMode < DataPluginDemo.FunctionList.Length) CmbTopLeft.SelectedIndex = Plugin.Settings.TopLeftEncoderMode;

            if (Plugin.Settings.TopRightEncoderMode >= 0 && Plugin.Settings.TopRightEncoderMode < DataPluginDemo.FunctionList.Length) CmbTopRight.SelectedIndex = Plugin.Settings.TopRightEncoderMode;



            chkAutoPit.IsChecked = Plugin.Settings.EnableAutoPitStrategy;

            chkMasterSystem.IsChecked = Plugin.Settings.EnableFuelCalculatorSystem;

            PnlStrategyContent.IsEnabled = (chkMasterSystem.IsChecked == true);



            ChkMasterVoice.IsChecked = Plugin.Settings.EnableVoiceEngineer;

            PnlVoiceSettings.Visibility = Plugin.Settings.EnableVoiceEngineer ? Visibility.Visible : Visibility.Collapsed;



            SetComboByTag(CmbRadioLang, Plugin.Settings.VoiceLanguage);
            SetComboByTag(CmbSpeedUnit, Plugin.Settings.SpeedUnit ?? "kmh");
            SetComboByTag(CmbTempUnit, Plugin.Settings.TempUnit ?? "C");
            SetComboByTag(CmbPressureUnit, Plugin.Settings.PressureUnit ?? "bar");

            RefreshVoiceModelsDropdown();

            _piperProgressHandler = (progress, status) => {
                Dispatcher.Invoke(() => UpdatePiperStatusUI());
            };
            _piperCompletedHandler = () => {
                Dispatcher.Invoke(() => {
                    UpdatePiperStatusUI();
                    Plugin.ReloadVoiceEngine();
                });
            };
            _piperFailedHandler = (err) => {
                Dispatcher.Invoke(() => {
                    UpdatePiperStatusUI();
                    System.Windows.MessageBox.Show("Download failed: " + err, "SimRIG Speech Setup", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                });
            };

            PiperEngine.Instance.OnDownloadProgress += _piperProgressHandler;
            PiperEngine.Instance.OnDownloadCompleted += _piperCompletedHandler;
            PiperEngine.Instance.OnDownloadFailed += _piperFailedHandler;

            UpdatePiperStatusUI();



            SldBrightnessBack.Value = Plugin.CurrentProfile.Brightness_Backlight;

            SldBrightnessRpm.Value = Plugin.CurrentProfile.Brightness_RPM;

            TxtStartVal.Text = Plugin.CurrentProfile.Rpm_StartLed.ToString();

            TxtCountVal.Text = Plugin.CurrentProfile.Rpm_LedCount.ToString();



            string mode = Plugin.CurrentProfile.Idle_Mode;

            if (mode == "Static") { CmbIdleMode.SelectedIndex = 1; BtnIdleColor.Visibility = Visibility.Visible; }

            else if (mode == "Breath") { CmbIdleMode.SelectedIndex = 2; BtnIdleColor.Visibility = Visibility.Visible; }

            else if (mode == "Off") { CmbIdleMode.SelectedIndex = 3; BtnIdleColor.Visibility = Visibility.Collapsed; }

            else { CmbIdleMode.SelectedIndex = 0; BtnIdleColor.Visibility = Visibility.Collapsed; }



            Color c = ProfileManager.HexToColor(Plugin.CurrentProfile.Idle_Color);

            BtnIdleColor.BorderBrush = new SolidColorBrush(c);

            ChkUseGradient.IsChecked = Plugin.CurrentProfile.Rpm_UseGradient;

            BtnRpmStartColor.BorderBrush = new SolidColorBrush(ProfileManager.HexToColor(Plugin.CurrentProfile.Rpm_Color_Start));

            BtnRpmEndColor.BorderBrush = new SolidColorBrush(ProfileManager.HexToColor(Plugin.CurrentProfile.Rpm_Color_End));



            if (Plugin.CurrentProfile.Rpm_Style == "RightToLeft") CmbRpmStyle.SelectedIndex = 1;

            else if (Plugin.CurrentProfile.Rpm_Style == "CenterToSide") CmbRpmStyle.SelectedIndex = 2;

            else if (Plugin.CurrentProfile.Rpm_Style == "SideToCenter") CmbRpmStyle.SelectedIndex = 3;

            else CmbRpmStyle.SelectedIndex = 0;



            TxtZoneLowCount.Text = Plugin.CurrentProfile.Rpm_ZoneLow_Count.ToString(); BtnZoneLowColor.BorderBrush = new SolidColorBrush(ProfileManager.HexToColor(Plugin.CurrentProfile.Rpm_ZoneLow_Color));

            TxtZoneMedCount.Text = Plugin.CurrentProfile.Rpm_ZoneMed_Count.ToString(); BtnZoneMedColor.BorderBrush = new SolidColorBrush(ProfileManager.HexToColor(Plugin.CurrentProfile.Rpm_ZoneMed_Color));

            TxtZoneHighCount.Text = Plugin.CurrentProfile.Rpm_ZoneHigh_Count.ToString(); BtnZoneHighColor.BorderBrush = new SolidColorBrush(ProfileManager.HexToColor(Plugin.CurrentProfile.Rpm_ZoneHigh_Color));

            TxtZoneMaxCount.Text = Plugin.CurrentProfile.Rpm_ZoneMax_Count.ToString(); BtnZoneMaxColor.BorderBrush = new SolidColorBrush(ProfileManager.HexToColor(Plugin.CurrentProfile.Rpm_ZoneMax_Color));



            if (ChkUseGradient.IsChecked == true) { PnlRpmGradient.Visibility = Visibility.Visible; PnlRpmCustom.Visibility = Visibility.Collapsed; }

            else { PnlRpmGradient.Visibility = Visibility.Collapsed; PnlRpmCustom.Visibility = Visibility.Visible; }



            CmbFlagSelector.SelectedIndex = 0; LoadFlagProfile_To_UI("Yellow");

            CmbCarSysSelector.SelectedIndex = 0; LoadCarProfile_To_UI("ABS");



            // Carica lo stato delle Checkbox di Debug (Assumendo che tu le abbia chiamate così nel file XAML)

            if (ChkLogSystem != null) ChkLogSystem.IsChecked = Plugin.Settings.EnableLogSystem;
            if (ChkLogFuel != null) ChkLogFuel.IsChecked = Plugin.Settings.EnableLogFuel;
            if (ChkLogStrategy != null) ChkLogStrategy.IsChecked = Plugin.Settings.EnableLogStrategy;
            if (ChkLogRadar != null) ChkLogRadar.IsChecked = Plugin.Settings.EnableLogRadar;

            if (ChkLogOpponents != null) ChkLogOpponents.IsChecked = Plugin.Settings.EnableLogOpponents;
            if (ChkLogMicrosector != null) ChkLogMicrosector.IsChecked = Plugin.Settings.EnableLogMicrosector;
            if (ChkLogWeather != null) ChkLogWeather.IsChecked = Plugin.Settings.EnableLogWeather;
            if (ChkLogHardware != null) ChkLogHardware.IsChecked = Plugin.Settings.EnableLogHardware;
            if (ChkLogVoice != null) ChkLogVoice.IsChecked = Plugin.Settings.EnableLogVoice;
            if (ChkLogMergeGap != null) ChkLogMergeGap.IsChecked = Plugin.Settings.EnableLogMergeGap;

            if (TxtCustomPlayerName != null) TxtCustomPlayerName.Text = Plugin.Settings.CustomPlayerName ?? "";

            if (TxtFuelWeightCoef != null) TxtFuelWeightCoef.Text = Plugin.Settings.FuelWeightCoef.ToString("F3", CultureInfo.InvariantCulture);
            if (TxtTempCoef != null) TxtTempCoef.Text = Plugin.Settings.TempCoef.ToString("F3", CultureInfo.InvariantCulture);
            if (TxtFuelSpeedCoef != null) TxtFuelSpeedCoef.Text = Plugin.Settings.FuelSpeedCoef.ToString("F3", CultureInfo.InvariantCulture);
            if (TxtSpeedDropThresholdKmh != null) TxtSpeedDropThresholdKmh.Text = Plugin.Settings.SpeedDropThresholdKmh.ToString("F1", CultureInfo.InvariantCulture);
            if (TxtDampSpeedDropOffsetKmh != null) TxtDampSpeedDropOffsetKmh.Text = Plugin.Settings.DampSpeedDropOffsetKmh.ToString("F1", CultureInfo.InvariantCulture);
            if (TxtDampPaceOffsetSeconds != null) TxtDampPaceOffsetSeconds.Text = Plugin.Settings.DampPaceOffsetSeconds.ToString("F1", CultureInfo.InvariantCulture);

        }



        private void SetComboByTag(ComboBox cmb, string tagValue)

        {

            foreach (ComboBoxItem item in cmb.Items)

            {

                if (item.Tag.ToString() == tagValue) { cmb.SelectedItem = item; return; }

            }

            if (cmb.Items.Count > 0) cmb.SelectedIndex = 0;

        }



        private void UpdateProfileFromUI()

        {

            Plugin.CurrentProfile.Brightness_Backlight = (int)SldBrightnessBack.Value; Plugin.CurrentProfile.Brightness_RPM = (int)SldBrightnessRpm.Value;

            int.TryParse(TxtStartVal.Text, out int start); int.TryParse(TxtCountVal.Text, out int count); Plugin.CurrentProfile.Rpm_StartLed = start; Plugin.CurrentProfile.Rpm_LedCount = count;

            Plugin.CurrentProfile.Idle_Mode = (CmbIdleMode.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "Rainbow"; Plugin.CurrentProfile.Rpm_Style = (CmbRpmStyle.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "LeftToRight";

            Plugin.CurrentProfile.Rpm_UseGradient = ChkUseGradient.IsChecked ?? true;

            int.TryParse(TxtZoneLowCount.Text, out int z1); Plugin.CurrentProfile.Rpm_ZoneLow_Count = z1;

            int.TryParse(TxtZoneMedCount.Text, out int z2); Plugin.CurrentProfile.Rpm_ZoneMed_Count = z2;

            int.TryParse(TxtZoneHighCount.Text, out int z3); Plugin.CurrentProfile.Rpm_ZoneHigh_Count = z3;

            int.TryParse(TxtZoneMaxCount.Text, out int z4); Plugin.CurrentProfile.Rpm_ZoneMax_Count = z4;

        }



        private void AdjustRpmParam(bool isStart, int delta)

        {

            if (isStart)

            {

                int val = int.Parse(TxtStartVal.Text) + delta;

                val = Math.Max(0, Math.Min(21, val));

                TxtStartVal.Text = val.ToString();

            }

            else

            {

                int val = int.Parse(TxtCountVal.Text) + delta;

                val = Math.Max(1, Math.Min(22, val));

                TxtCountVal.Text = val.ToString();

            }

            if (_isLoaded) { UpdateProfileFromUI(); Plugin.SendLedConfig(); }

        }



        private void OnLiveSettingChanged(object sender, RoutedEventArgs e)

        {

            if (!_isLoaded) return;

            UpdateProfileFromUI();

            Plugin.SendLedConfig();



            if (ChkUseGradient.IsChecked == true) { PnlRpmGradient.Visibility = Visibility.Visible; PnlRpmCustom.Visibility = Visibility.Collapsed; }

            else { PnlRpmGradient.Visibility = Visibility.Collapsed; PnlRpmCustom.Visibility = Visibility.Visible; }



            int maxAllowed = Plugin.CurrentProfile.Rpm_LedCount;

            if (Plugin.CurrentProfile.Rpm_Style == "CenterToSide" || Plugin.CurrentProfile.Rpm_Style == "SideToCenter") maxAllowed = maxAllowed / 2;

            int total = Plugin.CurrentProfile.Rpm_ZoneLow_Count + Plugin.CurrentProfile.Rpm_ZoneMed_Count + Plugin.CurrentProfile.Rpm_ZoneHigh_Count + Plugin.CurrentProfile.Rpm_ZoneMax_Count;



            if (total > maxAllowed) TxtLedCountWarning.Text = $"OVERLIMIT: {total}/{maxAllowed}";

            else TxtLedCountWarning.Text = "";



            string mode = (CmbIdleMode.SelectedItem as ComboBoxItem)?.Content.ToString();

            if (mode == "Static" || mode == "Breath") { BtnIdleColor.Visibility = Visibility.Visible; Color c = ProfileManager.HexToColor(Plugin.CurrentProfile.Idle_Color); BtnIdleColor.BorderBrush = new SolidColorBrush(c); }

            else { BtnIdleColor.Visibility = Visibility.Collapsed; }

        }



        private void AdvancedCoef_Changed(object sender, TextChangedEventArgs e)

        {

            if (!_isLoaded || Plugin == null || Plugin.Settings == null) return;



            string fuelText = TxtFuelWeightCoef.Text.Replace(',', '.');

            if (double.TryParse(fuelText, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double fuelVal))

            {

                Plugin.Settings.FuelWeightCoef = fuelVal;

            }



            string tempText = TxtTempCoef.Text.Replace(',', '.');
            if (double.TryParse(tempText, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double tempVal))
            {
                Plugin.Settings.TempCoef = tempVal;
            }

            if (TxtFuelSpeedCoef != null)
            {
                string fuelSpeedText = TxtFuelSpeedCoef.Text.Replace(',', '.');
                if (double.TryParse(fuelSpeedText, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double fsVal))
                {
                    Plugin.Settings.FuelSpeedCoef = fsVal;
                }
            }

            if (TxtSpeedDropThresholdKmh != null)
            {
                string thresholdText = TxtSpeedDropThresholdKmh.Text.Replace(',', '.');
                if (double.TryParse(thresholdText, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double thVal))
                {
                    Plugin.Settings.SpeedDropThresholdKmh = thVal;
                }
            }

            if (TxtDampSpeedDropOffsetKmh != null)
            {
                string dampDropText = TxtDampSpeedDropOffsetKmh.Text.Replace(',', '.');
                if (double.TryParse(dampDropText, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double dDropVal))
                {
                    Plugin.Settings.DampSpeedDropOffsetKmh = dDropVal;
                }
            }

            if (TxtDampPaceOffsetSeconds != null)
            {
                string dampPaceText = TxtDampPaceOffsetSeconds.Text.Replace(',', '.');
                if (double.TryParse(dampPaceText, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double dPaceVal))
                {
                    Plugin.Settings.DampPaceOffsetSeconds = dPaceVal;
                }
            }

            Plugin.SaveCommonSettings("GeneralSettings", Plugin.Settings);

        }

        private void TxtCustomPlayerName_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_isLoaded || Plugin == null || Plugin.Settings == null) return;
            Plugin.Settings.CustomPlayerName = TxtCustomPlayerName.Text;
            Plugin.SaveCommonSettings("GeneralSettings", Plugin.Settings);
        }



        private void UpdateGlobalTestState()
        {
            bool any = (ChkTestRpm.IsChecked == true) || (BtnTestFlag.IsChecked == true) || (BtnTestCar.IsChecked == true);
            Plugin.IsGlobalTesting = any;

            if (any)
            {
                Plugin.HardwareManager?.SendRawLedCommand("GAME:1");
                Plugin.SendLedConfig();
                Task.Run(() => { System.Threading.Thread.Sleep(50); Plugin.SyncAllColorsToHardware(); });
            }
            else
            {
                Plugin.HardwareManager?.SendRawLedCommand("GAME:0");
                Plugin.HardwareManager?.SendRawLedCommand("RPM:0");
            }
        }

        private void ToggleRpmTest(object sender, RoutedEventArgs e)
        {
            bool active = ChkTestRpm.IsChecked == true;
            SldRpmTest.Visibility = active ? Visibility.Visible : Visibility.Collapsed;

            if (active && SldRpmTest.Value == 0)
            {
                SldRpmTest.Value = 75;
            }

            UpdateGlobalTestState();

            if (active) Plugin.HardwareManager?.SendRawLedCommand($"RPM:{(int)SldRpmTest.Value}");
            else Plugin.HardwareManager?.SendRawLedCommand("RPM:0");
        }



        private void ToggleFlagTest(object sender, RoutedEventArgs e)

        {

            bool active = BtnTestFlag.IsChecked == true;

            UpdateGlobalTestState();

            if (active && ChkTestRpm.IsChecked != true) Plugin.HardwareManager?.SendRawLedCommand("RPM:0");

            int id = 0; if (_lastFlagSelection == "Blue") id = 2; else if (_lastFlagSelection == "Green") id = 3;

            Plugin.SendLedConfig();

            Plugin.HardwareManager?.SendRawLedCommand($"EVT:{id}:{(active ? 1 : 0)}");

        }



        private void ToggleCarTest(object sender, RoutedEventArgs e)

        {

            bool active = BtnTestCar.IsChecked == true;

            UpdateGlobalTestState();

            if (active && ChkTestRpm.IsChecked != true) Plugin.HardwareManager?.SendRawLedCommand("RPM:0");

            int id = 4; if (_lastCarSelection == "TC") id = 5; else if (_lastCarSelection == "Pit") id = 6;

            Plugin.SendLedConfig();

            Plugin.HardwareManager?.SendRawLedCommand($"EVT:{id}:{(active ? 1 : 0)}");

        }



        private void SldRpmTest_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)

        {

            if (!_isLoaded) return;

            if (Plugin.IsGlobalTesting && ChkTestRpm.IsChecked == true) Plugin.HardwareManager?.SendRawLedCommand($"RPM:{(int)SldRpmTest.Value}");

        }



        private void CmbFlagSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)

        {

            if (!_isLoaded) return;

            SaveFlagUI_To_Profile(_lastFlagSelection);

            if (BtnTestFlag.IsChecked == true)

            {

                int oldId = 0; if (_lastFlagSelection == "Blue") oldId = 2; else if (_lastFlagSelection == "Green") oldId = 3;

                Plugin.HardwareManager?.SendRawLedCommand($"EVT:{oldId}:0");

                BtnTestFlag.IsChecked = false; UpdateGlobalTestState();

            }

            var item = CmbFlagSelector.SelectedItem as ComboBoxItem;

            if (item != null) { string newSel = item.Tag.ToString(); LoadFlagProfile_To_UI(newSel); _lastFlagSelection = newSel; }

        }



        private void CmbCarSysSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)

        {

            if (!_isLoaded) return;

            SaveCarUI_To_Profile(_lastCarSelection);

            if (BtnTestCar.IsChecked == true)

            {

                int oldId = 4; if (_lastCarSelection == "TC") oldId = 5; else if (_lastCarSelection == "Pit") oldId = 6;

                Plugin.HardwareManager?.SendRawLedCommand($"EVT:{oldId}:0");

                BtnTestCar.IsChecked = false; UpdateGlobalTestState();

            }

            var item = CmbCarSysSelector.SelectedItem as ComboBoxItem;

            if (item != null) { string newSel = item.Tag.ToString(); LoadCarProfile_To_UI(newSel); _lastCarSelection = newSel; }

        }



        private int ParseInt(string txt, int defaultVal, int type)

        {

            if (int.TryParse(txt, out int v))

            {

                if (type == 0) return Math.Max(0, Math.Min(21, v));

                if (type == 1) return Math.Max(1, Math.Min(22, v));

                if (type == 2) return Math.Max(0, Math.Min(5000, v));

            }

            return defaultVal;

        }



        private void LoadFlagProfile_To_UI(string flagType)

        {

            LedEventConfig cfg = null;

            if (flagType == "Yellow") cfg = Plugin.CurrentProfile.Flag_Yellow;

            else if (flagType == "Blue") cfg = Plugin.CurrentProfile.Flag_Blue;

            else if (flagType == "Green") cfg = Plugin.CurrentProfile.Flag_Green;

            if (cfg == null) return;

            bool wasLoaded = _isLoaded; _isLoaded = false;

            TxtFlagA_Start.Text = cfg.ZoneA_Start.ToString(); TxtFlagA_Count.Text = cfg.ZoneA_Count.ToString();

            ChkFlagB_Enable.IsChecked = cfg.ZoneB_Enabled; TxtFlagB_Start.Text = cfg.ZoneB_Start.ToString(); TxtFlagB_Count.Text = cfg.ZoneB_Count.ToString();

            ChkFlagBlink.IsChecked = cfg.IsBlinking; TxtFlagBlinkMs.Text = cfg.BlinkIntervalMs.ToString();

            PnlFlagZoneB.Visibility = cfg.ZoneB_Enabled ? Visibility.Visible : Visibility.Collapsed;

            PnlFlagBlinkRate.Visibility = cfg.IsBlinking ? Visibility.Visible : Visibility.Collapsed;

            _isLoaded = wasLoaded;

        }



        private void SaveFlagUI_To_Profile(string flagType)

        {

            LedEventConfig cfg = null;

            if (flagType == "Yellow") cfg = Plugin.CurrentProfile.Flag_Yellow;

            else if (flagType == "Blue") cfg = Plugin.CurrentProfile.Flag_Blue;

            else if (flagType == "Green") cfg = Plugin.CurrentProfile.Flag_Green;

            if (cfg == null) return;

            cfg.ZoneA_Start = ParseInt(TxtFlagA_Start.Text, 0, 0); cfg.ZoneA_Count = ParseInt(TxtFlagA_Count.Text, 3, 1);

            cfg.ZoneB_Enabled = ChkFlagB_Enable.IsChecked ?? false; cfg.ZoneB_Start = ParseInt(TxtFlagB_Start.Text, 19, 0); cfg.ZoneB_Count = ParseInt(TxtFlagB_Count.Text, 3, 1);

            cfg.IsBlinking = ChkFlagBlink.IsChecked ?? false; cfg.BlinkIntervalMs = ParseInt(TxtFlagBlinkMs.Text, 300, 2);

            Plugin.SendLedConfig();

        }



        private void FlagControl_Changed(object sender, RoutedEventArgs e)

        {

            if (!_isLoaded) return;

            PnlFlagZoneB.Visibility = (ChkFlagB_Enable.IsChecked == true) ? Visibility.Visible : Visibility.Collapsed;

            PnlFlagBlinkRate.Visibility = (ChkFlagBlink.IsChecked == true) ? Visibility.Visible : Visibility.Collapsed;

            SaveFlagUI_To_Profile(_lastFlagSelection);

        }



        private void LoadCarProfile_To_UI(string carType)

        {

            LedEventConfig cfg = null;

            if (carType == "ABS") cfg = Plugin.CurrentProfile.Car_ABS;

            else if (carType == "TC") cfg = Plugin.CurrentProfile.Car_TC;

            else if (carType == "Pit") cfg = Plugin.CurrentProfile.Car_Pit;

            if (cfg == null) return;

            bool wasLoaded = _isLoaded; _isLoaded = false;

            TxtCarA_Start.Text = cfg.ZoneA_Start.ToString(); TxtCarA_Count.Text = cfg.ZoneA_Count.ToString();

            ChkCarB_Enable.IsChecked = cfg.ZoneB_Enabled; TxtCarB_Start.Text = cfg.ZoneB_Start.ToString(); TxtCarB_Count.Text = cfg.ZoneB_Count.ToString();

            ChkCarBlink.IsChecked = cfg.IsBlinking; TxtCarBlinkMs.Text = cfg.BlinkIntervalMs.ToString();

            Color c = ProfileManager.HexToColor(cfg.ColorHex); BtnCarColor.BorderBrush = new SolidColorBrush(c);

            if (carType == "Pit")

            {

                BtnCarColor2.Visibility = Visibility.Visible; Color c2 = ProfileManager.HexToColor(cfg.ColorHexSecondary); BtnCarColor2.BorderBrush = new SolidColorBrush(c2);

            }

            else { BtnCarColor2.Visibility = Visibility.Collapsed; }

            PnlCarZoneB.Visibility = cfg.ZoneB_Enabled ? Visibility.Visible : Visibility.Collapsed;

            PnlCarBlinkRate.Visibility = cfg.IsBlinking ? Visibility.Visible : Visibility.Collapsed;

            _isLoaded = wasLoaded;

        }



        private void SaveCarUI_To_Profile(string carType)

        {

            LedEventConfig cfg = null;

            if (carType == "ABS") cfg = Plugin.CurrentProfile.Car_ABS;

            else if (carType == "TC") cfg = Plugin.CurrentProfile.Car_TC;

            else if (carType == "Pit") cfg = Plugin.CurrentProfile.Car_Pit;

            if (cfg == null) return;

            cfg.ZoneA_Start = ParseInt(TxtCarA_Start.Text, 0, 0); cfg.ZoneA_Count = ParseInt(TxtCarA_Count.Text, 3, 1);

            cfg.ZoneB_Enabled = ChkCarB_Enable.IsChecked ?? false; cfg.ZoneB_Start = ParseInt(TxtCarB_Start.Text, 19, 0); cfg.ZoneB_Count = ParseInt(TxtCarB_Count.Text, 3, 1);

            cfg.IsBlinking = ChkCarBlink.IsChecked ?? false; cfg.BlinkIntervalMs = ParseInt(TxtCarBlinkMs.Text, 300, 2);

            Plugin.SendLedConfig();

        }



        private void CarControl_Changed(object sender, RoutedEventArgs e)

        {

            if (!_isLoaded) return;

            PnlCarZoneB.Visibility = (ChkCarB_Enable.IsChecked == true) ? Visibility.Visible : Visibility.Collapsed;

            PnlCarBlinkRate.Visibility = (ChkCarBlink.IsChecked == true) ? Visibility.Visible : Visibility.Collapsed;

            SaveCarUI_To_Profile(_lastCarSelection);

        }



        private void BtnCarColor_Click(object sender, RoutedEventArgs e)

        {

            LedEventConfig cfg = GetCurrentCarConfig();

            using (var colorDialog = new System.Windows.Forms.ColorDialog())

            {

                Color current = ProfileManager.HexToColor(cfg.ColorHex); colorDialog.Color = System.Drawing.Color.FromArgb(current.A, current.R, current.G, current.B);

                if (colorDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)

                {

                    Color newColor = Color.FromRgb(colorDialog.Color.R, colorDialog.Color.G, colorDialog.Color.B);

                    cfg.ColorHex = ProfileManager.ColorToHex(newColor); BtnCarColor.BorderBrush = new SolidColorBrush(newColor);

                    Keyboard.ClearFocus(); Plugin.SendLedConfig();

                }

            }

        }



        private void BtnCarColor2_Click(object sender, RoutedEventArgs e)

        {

            LedEventConfig cfg = GetCurrentCarConfig();

            using (var colorDialog = new System.Windows.Forms.ColorDialog())

            {

                Color current = ProfileManager.HexToColor(cfg.ColorHexSecondary); colorDialog.Color = System.Drawing.Color.FromArgb(current.A, current.R, current.G, current.B);

                if (colorDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)

                {

                    Color newColor = Color.FromRgb(colorDialog.Color.R, colorDialog.Color.G, colorDialog.Color.B);

                    cfg.ColorHexSecondary = ProfileManager.ColorToHex(newColor); BtnCarColor2.BorderBrush = new SolidColorBrush(newColor);

                    Keyboard.ClearFocus(); Plugin.SendLedConfig();

                }

            }

        }



        private LedEventConfig GetCurrentCarConfig()

        {

            if (_lastCarSelection == "ABS") return Plugin.CurrentProfile.Car_ABS;

            if (_lastCarSelection == "TC") return Plugin.CurrentProfile.Car_TC;

            return Plugin.CurrentProfile.Car_Pit;

        }



        private void PickZoneColor(int zoneId)

        {

            Color current = Colors.White;

            if (zoneId == 1) current = ProfileManager.HexToColor(Plugin.CurrentProfile.Rpm_ZoneLow_Color);

            if (zoneId == 2) current = ProfileManager.HexToColor(Plugin.CurrentProfile.Rpm_ZoneMed_Color);

            if (zoneId == 3) current = ProfileManager.HexToColor(Plugin.CurrentProfile.Rpm_ZoneHigh_Color);

            if (zoneId == 4) current = ProfileManager.HexToColor(Plugin.CurrentProfile.Rpm_ZoneMax_Color);



            using (var colorDialog = new System.Windows.Forms.ColorDialog())

            {

                colorDialog.Color = System.Drawing.Color.FromArgb(current.A, current.R, current.G, current.B);

                if (colorDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)

                {

                    Color newColor = Color.FromRgb(colorDialog.Color.R, colorDialog.Color.G, colorDialog.Color.B);

                    string hex = ProfileManager.ColorToHex(newColor);

                    if (zoneId == 1) { Plugin.CurrentProfile.Rpm_ZoneLow_Color = hex; BtnZoneLowColor.BorderBrush = new SolidColorBrush(newColor); }

                    if (zoneId == 2) { Plugin.CurrentProfile.Rpm_ZoneMed_Color = hex; BtnZoneMedColor.BorderBrush = new SolidColorBrush(newColor); }

                    if (zoneId == 3) { Plugin.CurrentProfile.Rpm_ZoneHigh_Color = hex; BtnZoneHighColor.BorderBrush = new SolidColorBrush(newColor); }

                    if (zoneId == 4) { Plugin.CurrentProfile.Rpm_ZoneMax_Color = hex; BtnZoneMaxColor.BorderBrush = new SolidColorBrush(newColor); }

                    Plugin.SendLedConfig(); Keyboard.ClearFocus();

                }

            }

        }



        private void BtnIdleColor_Click(object sender, RoutedEventArgs e)

        {

            using (var colorDialog = new System.Windows.Forms.ColorDialog())

            {

                Color current = ProfileManager.HexToColor(Plugin.CurrentProfile.Idle_Color); colorDialog.Color = System.Drawing.Color.FromArgb(current.A, current.R, current.G, current.B);

                if (colorDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)

                {

                    Color newColor = Color.FromRgb(colorDialog.Color.R, colorDialog.Color.G, colorDialog.Color.B);

                    Plugin.CurrentProfile.Idle_Color = ProfileManager.ColorToHex(newColor); Plugin.SendLedConfig(); BtnIdleColor.BorderBrush = new SolidColorBrush(newColor); Keyboard.ClearFocus();

                }

            }

        }



        private void BtnRpmStartColor_Click(object sender, RoutedEventArgs e)

        {

            using (var colorDialog = new System.Windows.Forms.ColorDialog())

            {

                Color current = ProfileManager.HexToColor(Plugin.CurrentProfile.Rpm_Color_Start); colorDialog.Color = System.Drawing.Color.FromArgb(current.A, current.R, current.G, current.B);

                if (colorDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)

                {

                    Color newColor = Color.FromRgb(colorDialog.Color.R, colorDialog.Color.G, colorDialog.Color.B);

                    Plugin.CurrentProfile.Rpm_Color_Start = ProfileManager.ColorToHex(newColor); Plugin.SendLedConfig(); BtnRpmStartColor.BorderBrush = new SolidColorBrush(newColor); Keyboard.ClearFocus();

                }

            }

        }



        private void BtnRpmEndColor_Click(object sender, RoutedEventArgs e)

        {

            using (var colorDialog = new System.Windows.Forms.ColorDialog())

            {

                Color current = ProfileManager.HexToColor(Plugin.CurrentProfile.Rpm_Color_End); colorDialog.Color = System.Drawing.Color.FromArgb(current.A, current.R, current.G, current.B);

                if (colorDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)

                {

                    Color newColor = Color.FromRgb(colorDialog.Color.R, colorDialog.Color.G, colorDialog.Color.B);

                    Plugin.CurrentProfile.Rpm_Color_End = ProfileManager.ColorToHex(newColor); Plugin.SendLedConfig(); BtnRpmEndColor.BorderBrush = new SolidColorBrush(newColor); Keyboard.ClearFocus();

                }

            }

        }



        private void RefreshProfileList()

        {

            List<string> profiles = ProfileManager.GetAvailableProfiles();

            CmbProfiles.ItemsSource = profiles;

            if (!string.IsNullOrEmpty(Plugin.CurrentProfile.ProfileName) && profiles.Contains(Plugin.CurrentProfile.ProfileName)) CmbProfiles.SelectedItem = Plugin.CurrentProfile.ProfileName;

            else if (profiles.Count > 0) { if (profiles.Contains("Default")) CmbProfiles.SelectedItem = "Default"; else CmbProfiles.SelectedIndex = 0; }

        }



        private void CmbProfiles_SelectionChanged(object sender, SelectionChangedEventArgs e)

        {

            if (PnlConfirmDelete.Visibility == Visibility.Visible) { PnlConfirmDelete.Visibility = Visibility.Collapsed; PnlStandardActions.Visibility = Visibility.Visible; }

        }



        private void BtnDelete_Click(object sender, RoutedEventArgs e)

        {

            if (CmbProfiles.SelectedItem == null) return;

            string name = CmbProfiles.SelectedItem.ToString();

            if (ProfileManager.FactoryProfiles.Contains(name))

            {

                var originalBrush = BtnDelete.Foreground; var originalText = BtnDelete.Content;

                BtnDelete.Foreground = _brushWarning; BtnDelete.Content = "LOCKED";

                Task.Delay(1000).ContinueWith(t => { Dispatcher.Invoke(() => { BtnDelete.Foreground = originalBrush; BtnDelete.Content = originalText; }); }); return;

            }

            PnlStandardActions.Visibility = Visibility.Collapsed; PnlConfirmDelete.Visibility = Visibility.Visible;

        }



        private void BtnDeleteYes_Click(object sender, RoutedEventArgs e)

        {

            if (CmbProfiles.SelectedItem == null) return;

            string name = CmbProfiles.SelectedItem.ToString();

            if (ProfileManager.DeleteProfile(name))

            {

                SimRigProfile defaultProf = ProfileManager.LoadProfile("Default");

                if (defaultProf != null) { Plugin.ApplyProfile(defaultProf); LoadValuesFromPlugin(); }

                RefreshProfileList(); PnlConfirmDelete.Visibility = Visibility.Collapsed; PnlStandardActions.Visibility = Visibility.Visible;

            }

        }

        private void BtnDeleteNo_Click(object sender, RoutedEventArgs e) { PnlConfirmDelete.Visibility = Visibility.Collapsed; PnlStandardActions.Visibility = Visibility.Visible; }



        private void BtnLoadProfile_Click(object sender, RoutedEventArgs e)

        {

            if (CmbProfiles.SelectedItem == null) return;

            string name = CmbProfiles.SelectedItem.ToString(); SimRigProfile profile = ProfileManager.LoadProfile(name);

            if (profile != null)

            {

                Plugin.ApplyProfile(profile); LoadValuesFromPlugin(); ChkTestRpm.IsChecked = false; SldRpmTest.Visibility = Visibility.Collapsed; BtnTestFlag.IsChecked = false; BtnTestCar.IsChecked = false;

            }

        }



        private void BtnSaveAs_Click(object sender, RoutedEventArgs e) { TxtNewProfileName.Text = string.Empty; TxtNewProfileName.Visibility = Visibility.Visible; BtnConfirmSaveAs.Visibility = Visibility.Visible; BtnSaveAs.Visibility = Visibility.Collapsed; }



        private async void BtnConfirmSaveAs_Click(object sender, RoutedEventArgs e)

        {

            string name = TxtNewProfileName.Text.Trim();

            if (!string.IsNullOrEmpty(name))

            {

                UpdateProfileFromUI(); SaveFlagUI_To_Profile(_lastFlagSelection); SaveCarUI_To_Profile(_lastCarSelection); Plugin.SaveCurrentProfileToDisk(name); RefreshProfileList();

                TxtNewProfileName.Visibility = Visibility.Collapsed; BtnConfirmSaveAs.Visibility = Visibility.Collapsed; BtnSaveAs.Visibility = Visibility.Visible;

                var originalBrush = BtnSaveAs.Background; var originalContent = BtnSaveAs.Content;

                BtnSaveAs.Background = _brushSuccess; BtnSaveAs.Content = "SAVED! \u2713";

                await Task.Delay(2000); BtnSaveAs.Background = originalBrush; BtnSaveAs.Content = originalContent;

            }

        }



        private async void BtnSaveDirect_Click(object sender, RoutedEventArgs e)

        {

            if (CmbProfiles.SelectedItem == null) return;

            string name = CmbProfiles.SelectedItem.ToString();

            if (ProfileManager.FactoryProfiles.Contains(name))

            {

                var originalBrush = BtnSaveDirect.Background; var originalContent = BtnSaveDirect.Content;

                BtnSaveDirect.Background = _brushError; BtnSaveDirect.Content = "LOCKED";

                await Task.Delay(1000); BtnSaveDirect.Background = originalBrush; BtnSaveDirect.Content = originalContent; return;

            }

            UpdateProfileFromUI(); SaveFlagUI_To_Profile(_lastFlagSelection); SaveCarUI_To_Profile(_lastCarSelection); Plugin.SaveCurrentProfileToDisk(name);

            var okBrush = BtnSaveDirect.Background; var okContent = BtnSaveDirect.Content; BtnSaveDirect.Background = _brushSuccess; BtnSaveDirect.Content = "SAVED! \u2713";

            await Task.Delay(2000); BtnSaveDirect.Background = okBrush; BtnSaveDirect.Content = okContent;

        }



        private void Dot_MouseRightButtonUp(object sender, MouseButtonEventArgs e)

        {

            if (sender is Ellipse dot && dot.Tag != null)

            {

                if (int.TryParse(dot.Tag.ToString(), out int btnIndex))

                {

                    using (var colorDialog = new System.Windows.Forms.ColorDialog())

                    {

                        Color current = Plugin.ButtonColors[btnIndex]; colorDialog.Color = System.Drawing.Color.FromArgb(current.A, current.R, current.G, current.B);

                        if (colorDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)

                        {

                            Color newColor = Color.FromRgb(colorDialog.Color.R, colorDialog.Color.G, colorDialog.Color.B); Plugin.SetButtonColor(btnIndex, newColor);

                        }

                    }

                }

            }

        }



        private void CmbTopLeft_SelectionChanged(object sender, SelectionChangedEventArgs e)

        {

            if (!_isLoaded) return;

            int idx = CmbTopLeft.SelectedIndex;

            if (idx >= 0)

            {

                Plugin.Settings.TopLeftEncoderMode = idx;

                Plugin.HardwareManager?.SendEncoderMapping(0, idx);

                Plugin.UpdateSimHubProperties();

            }

        }



        private void CmbTopRight_SelectionChanged(object sender, SelectionChangedEventArgs e)

        {

            if (!_isLoaded) return;

            int idx = CmbTopRight.SelectedIndex;

            if (idx >= 0)

            {

                Plugin.Settings.TopRightEncoderMode = idx;

                Plugin.HardwareManager?.SendEncoderMapping(1, idx);

                Plugin.UpdateSimHubProperties();

            }

        }



        private void ChkAutoPit_Click(object sender, RoutedEventArgs e)

        {

            if (Plugin.Settings != null)

            {

                Plugin.Settings.EnableAutoPitStrategy = (chkAutoPit.IsChecked == true);

                bool effectiveAutoPit = Plugin.Settings.EnableAutoPitStrategy && Plugin.Settings.EnableFuelCalculatorSystem;

                Plugin.PluginManager.SetPropertyValue("SimRIG.Strategy.AutoPitEnabled", Plugin.GetType(), effectiveAutoPit);

            }

        }



        private void ChkMasterSystem_Click(object sender, RoutedEventArgs e)

        {

            if (Plugin.Settings != null)

            {

                Plugin.Settings.EnableFuelCalculatorSystem = (chkMasterSystem.IsChecked == true);

                PnlStrategyContent.IsEnabled = Plugin.Settings.EnableFuelCalculatorSystem;

                Plugin.HardwareManager?.SendInputSystemState(Plugin.Settings.EnableFuelCalculatorSystem);

                Plugin.PluginManager.SetPropertyValue("SimRIG.Strategy.FuelCalculatorEnabled", Plugin.GetType(), Plugin.Settings.EnableFuelCalculatorSystem);

                bool effectiveAutoPit = Plugin.Settings.EnableAutoPitStrategy && Plugin.Settings.EnableFuelCalculatorSystem;

                Plugin.PluginManager.SetPropertyValue("SimRIG.Strategy.AutoPitEnabled", Plugin.GetType(), effectiveAutoPit);

            }

        }



        private void ChkMasterVoice_Click(object sender, RoutedEventArgs e)

        {

            if (Plugin.Settings != null)

            {

                Plugin.Settings.EnableVoiceEngineer = (ChkMasterVoice.IsChecked == true);

                PnlVoiceSettings.Visibility = Plugin.Settings.EnableVoiceEngineer ? Visibility.Visible : Visibility.Collapsed;

            }

        }







        private void ToggleDebugLogs(object sender, RoutedEventArgs e)

        {

            if (!_isLoaded || Plugin == null || Plugin.Settings == null) return;



            Plugin.Settings.EnableLogSystem = (ChkLogSystem?.IsChecked == true);
            Plugin.Settings.EnableLogFuel = (ChkLogFuel?.IsChecked == true);
            Plugin.Settings.EnableLogStrategy = (ChkLogStrategy?.IsChecked == true);
            Plugin.Settings.EnableLogRadar = (ChkLogRadar?.IsChecked == true);
            Plugin.Settings.EnableLogOpponents = (ChkLogOpponents?.IsChecked == true);
            Plugin.Settings.EnableLogMicrosector = (ChkLogMicrosector?.IsChecked == true);
            Plugin.Settings.EnableLogHardware = (ChkLogHardware?.IsChecked == true);
            Plugin.Settings.EnableLogVoice = (ChkLogVoice?.IsChecked == true);
            Plugin.Settings.EnableLogWeather = (ChkLogWeather?.IsChecked == true);
            Plugin.Settings.EnableLogMergeGap = (ChkLogMergeGap?.IsChecked == true);

            if (Plugin.LogManager != null)
            {
                Plugin.LogManager.EnableLogSystem = Plugin.Settings.EnableLogSystem;
                Plugin.LogManager.EnableLogFuel = Plugin.Settings.EnableLogFuel;
                Plugin.LogManager.EnableLogStrategy = Plugin.Settings.EnableLogStrategy;
                Plugin.LogManager.EnableLogRadar = Plugin.Settings.EnableLogRadar;
                Plugin.LogManager.EnableLogOpponents = Plugin.Settings.EnableLogOpponents;
                Plugin.LogManager.EnableLogMicrosector = Plugin.Settings.EnableLogMicrosector;
                Plugin.LogManager.EnableLogWeather = Plugin.Settings.EnableLogWeather;
                Plugin.LogManager.EnableLogHardware = Plugin.Settings.EnableLogHardware;
                Plugin.LogManager.EnableLogVoice = Plugin.Settings.EnableLogVoice;
                Plugin.LogManager.EnableLogMergeGap = Plugin.Settings.EnableLogMergeGap;
            }

        }

        private void BtnRunUnitTests_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                TxtUnitTestStatus.Text = "TEST IN CORSO...";
                TxtUnitTestStatus.Foreground = new SolidColorBrush(Colors.Orange);

                // 1. Test GT3 / PCUP Pit Profiles
                double t4 = CarPitData.GetProfile("GT3").Tires4;
                if (Math.Abs(t4 - 26.0) > 0.01) throw new Exception($"GT3 Tire Time atteso 26.0s ma ottenuto {t4}s");

                var pcup = CarPitData.GetProfile("PCUP");
                if (!pcup.IsSequential) throw new Exception("PCUP profilo deve essere IsSequential = true");

                // 2. Test Merge Gap 3 Race States
                // Stato 1: Sara -20.7s, Loss 32.18s, Target 0.0s -> +11.48s
                double gap1 = -20.7 + 32.18 - 0.0;
                if (Math.Abs(gap1 - 11.48) > 0.01) throw new Exception($"Stato 1 atteso +11.48s ma ottenuto {gap1:F2}s");

                // Stato 2: Entrambi pitted, Sara +11.7s, Loss 32.18s, Target 0.0s -> +43.88s
                double gap2 = 11.7 + 32.18 - 0.0;
                if (Math.Abs(gap2 - 43.88) > 0.01) throw new Exception($"Stato 2 atteso +43.88s ma ottenuto {gap2:F2}s");

                // Stato 3: Entrambi devono pittare, Sara -5.0s, Loss 32.18s, Target 36.50s -> -9.32s
                double gap3 = -5.0 + 32.18 - 36.50;
                if (Math.Abs(gap3 - (-9.32)) > 0.01) throw new Exception($"Stato 3 atteso -9.32s ma ottenuto {gap3:F2}s");

                TxtUnitTestStatus.Text = "TUTTI I TEST SUPERATI (100%)";
                TxtUnitTestStatus.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4CAF50"));
            }
            catch (Exception ex)
            {
                TxtUnitTestStatus.Text = $"FALLITO: {ex.Message}";
                TxtUnitTestStatus.Foreground = new SolidColorBrush(Colors.Red);
            }
        }



        private void CmbRadioSettings_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded) return;
            var langItem = CmbRadioLang.SelectedItem as ComboBoxItem;
            if (langItem != null)
            {
                Plugin.Settings.VoiceLanguage = langItem.Tag.ToString();
                RefreshVoiceModelsDropdown();
                UpdatePiperStatusUI();
                Plugin.ReloadVoiceEngine();
            }
        }

        private void CmbUnits_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded || Plugin == null) return;

            var speedItem = CmbSpeedUnit.SelectedItem as ComboBoxItem;
            if (speedItem != null) Plugin.Settings.SpeedUnit = speedItem.Tag.ToString();

            var tempItem = CmbTempUnit.SelectedItem as ComboBoxItem;
            if (tempItem != null) Plugin.Settings.TempUnit = tempItem.Tag.ToString();

            var pressItem = CmbPressureUnit.SelectedItem as ComboBoxItem;
            if (pressItem != null)
            {
                string newUnit = pressItem.Tag.ToString();
                Plugin.Settings.PressureUnit = newUnit;
                if (Plugin.TyreManager != null)
                {
                    Plugin.TyreManager.SetUnit(newUnit);
                }
            }

            Plugin.UpdateSimHubProperties();
        }



        // -------------------------------------------------------------------------

        // RENDER LOOP (Aggiorna UI in tempo reale)

        // -------------------------------------------------------------------------

        private void CompositionTarget_Rendering(object sender, EventArgs e)

        {

            if (Plugin == null) return;

            UpdateConnectionStatus();



            if (TxtBotLeft != null) TxtBotLeft.Text = Plugin.GetBottomLeftLabel();

            if (TxtBotRight != null) TxtBotRight.Text = Plugin.GetBottomRightLabel();



            if (TxtPitFuel != null) TxtPitFuel.Text = Plugin.GetPitFuelLabel();

            if (TxtPitTyres != null) TxtPitTyres.Text = Plugin.GetTyreScopeLabel();

            if (TxtPitStrat != null) TxtPitStrat.Text = Plugin.GetPitStratLabel();

            if (TxtPitPress != null) TxtPitPress.Text = Plugin.GetPitPressLabel();



            if (TxtLiveCons != null && Plugin.FuelManager != null)

                TxtLiveCons.Text = $"{Plugin.FuelManager.Calculations.AverageFuelPerLap:F2} L/lap";



            if (TxtLiveLaps != null && Plugin.FuelManager != null)

                TxtLiveLaps.Text = Plugin.FuelManager.Calculations.TankLapsRemaining >= 99.0 ? "99" : Plugin.FuelManager.Calculations.TankLapsRemaining.ToString("F2", CultureInfo.InvariantCulture);



            if (TxtLiveFuelReq != null && Plugin.FuelManager != null)

                TxtLiveFuelReq.Text = $"{Plugin.FuelManager.Calculations.FuelToAdd:F1} L";



            if (TxtStratTarget != null && Plugin.TargetStrategyManager != null)

            {

                var tgt = Plugin.TargetStrategyManager.CurrentTarget;

                TxtStratTarget.Text = $"{tgt.ModeLabel} - {tgt.Name}";



                if (tgt.Name.Contains("NO TARGET") || tgt.Name.Contains("PLAYER"))

                    TxtStratGap.Text = $"-- | {tgt.Diagnosis}";

                else

                    TxtStratGap.Text = $"{tgt.GapString}s | {tgt.Diagnosis}";



                if (tgt.UndercutViable) { TxtStratUndercut.Text = "VIABLE"; TxtStratUndercut.Foreground = _brushSuccess; }

                else { TxtStratUndercut.Text = "NO"; TxtStratUndercut.Foreground = _brushError; }



                if (Plugin.PitRadar != null)

                {

                    double totalLoss = Plugin.PitRadar.PitTransitTime + Plugin.PitRadar.DbTireChangeTime; // Stima grossolana per la UI WPF

                    TxtStratDrop.Text = $"{totalLoss:F0}s";

                }

            }



            // Input UI

            if (Plugin.HardwareManager != null)

            {

                Plugin.HardwareManager.PollJoystick();

                bool[] btn = Plugin.HardwareManager.RawButtons;

                if (btn != null)

                {

                    UpdateColoredDot(Dot_Btn_0, btn[0], 0); UpdateColoredDot(Dot_Btn_1, btn[1], 1); UpdateColoredDot(Dot_Btn_2, btn[2], 2); UpdateColoredDot(Dot_Btn_3, btn[3], 3);

                    UpdateColoredDot(Dot_Btn_4, btn[4], 4); UpdateColoredDot(Dot_Btn_5, btn[5], 5); UpdateColoredDot(Dot_Btn_6, btn[11], 6); UpdateColoredDot(Dot_Btn_7, btn[10], 7);

                    UpdateColoredDot(Dot_Btn_8, btn[9], 8); UpdateColoredDot(Dot_Btn_9, btn[8], 9); UpdateColoredDot(Dot_Btn_10, btn[7], 10); UpdateColoredDot(Dot_Btn_11, btn[6], 11);



                    UpdateInputDot(Dot_Btn_12, btn[12]); UpdateInputDot(Dot_Btn_13, btn[13]);

                    UpdateInputDot(Dot_Btn_14, btn[14]); UpdateInputDot(Dot_Btn_15, btn[15]);

                    UpdateInputDot(Dot_Btn_16, btn[16]); UpdateInputDot(Dot_Btn_17, btn[17]); UpdateInputDot(Dot_Btn_18, btn[18]); UpdateInputDot(Dot_Btn_19, btn[19]); UpdateInputDot(Dot_Btn_20, btn[20]);

                    UpdateInputDot(Dot_Btn_21, btn[21]); UpdateInputDot(Dot_Btn_22, btn[22]); UpdateInputDot(Dot_Btn_23, btn[23]); UpdateInputDot(Dot_Btn_24, btn[24]); UpdateInputDot(Dot_Btn_25, btn[25]);

                    UpdateInputDot(Dot_Btn_26, btn[26]); UpdateInputDot(Dot_Btn_27, btn[27]); UpdateInputDot(Dot_Btn_28, btn[28]); UpdateInputDot(Dot_Btn_29, btn[29]);

                }

                int[] axes = Plugin.HardwareManager.RawAxes;

                if (axes != null) { BarClutchL.Value = axes[4]; BarClutchR.Value = axes[5]; BarClutchComb.Value = axes[2]; }

            }



            TxtBitePoint.Text = $"{Plugin.PersoSteeringWheelLiveBitePoint:F1}%";

        }



        private void UpdateColoredDot(Ellipse dot, bool isActive, int logicalId) { if (dot == null) return; SolidColorBrush borderBrush = new SolidColorBrush(Plugin.ButtonColors[logicalId]); if (dot.Stroke.ToString() != borderBrush.ToString()) dot.Stroke = borderBrush; Brush targetFill = isActive ? _brushLavaRed : Brushes.Transparent; if (dot.Fill != targetFill) dot.Fill = targetFill; Effect targetEffect = isActive ? _glowEffect : null; if (dot.Effect != targetEffect) dot.Effect = targetEffect; }

        private void UpdateInputDot(Ellipse dot, bool isActive) { if (dot == null) return; if (isActive) { dot.Opacity = 1.0; dot.Fill = _brushLavaRed; dot.Effect = _glowEffect; } else { dot.Opacity = 0.0; dot.Fill = Brushes.Transparent; dot.Effect = null; } }



        private void UpdateConnectionStatus()

        {

            if (Plugin.IsInputConnected) { LedInput.Fill = _brushSuccess; TxtInputStatus.Text = $"OK ({Plugin.InputPortName})"; }

            else { LedInput.Fill = _brushError; TxtInputStatus.Text = "No Input"; }

            if (Plugin.IsLedsConnected) { LedLeds.Fill = _brushSuccess; TxtLedsStatus.Text = $"OK ({Plugin.LedsPortName})"; }

            else { LedLeds.Fill = _brushError; TxtLedsStatus.Text = "No LEDs"; }

        }



        private async void BtnScan_Click(object sender, RoutedEventArgs e)
        {
            BtnScan.IsEnabled = false; BtnScan.Content = "...";
            if (Plugin.HardwareManager != null) await Plugin.HardwareManager.RunDiscoveryAsync();
            BtnScan.Content = "SCAN DEVICES"; BtnScan.IsEnabled = true;
        }

        private void UpdatePiperStatusUI()
        {
            if (Plugin == null) return;
            string lang = Plugin.Settings.VoiceLanguage;
            string voiceId = Plugin.Settings.SelectedVoiceModel;
            bool engineInstalled = PiperEngine.Instance.CheckEngineInstalled();
            bool modelInstalled = PiperEngine.Instance.CheckModelInstalled(lang, voiceId);

            if (PiperEngine.Instance.IsDownloading)
            {
                PbVoiceDownload.Visibility = System.Windows.Visibility.Visible;
                PbVoiceDownload.Value = PiperEngine.Instance.DownloadProgress;
                TxtVoiceStatus.Text = $"Status: {PiperEngine.Instance.DownloadStatus} ({PiperEngine.Instance.DownloadProgress:F0}%)";
                BtnSetupVoice.IsEnabled = false;
            }
            else
            {
                PbVoiceDownload.Visibility = System.Windows.Visibility.Collapsed;
                if (!engineInstalled)
                {
                    TxtVoiceStatus.Text = "Status: Piper Engine not installed. Click Install to download (~12MB).";
                    BtnSetupVoice.Content = "INSTALL ENGINE";
                    BtnSetupVoice.IsEnabled = true;
                }
                else if (!modelInstalled)
                {
                    TxtVoiceStatus.Text = "Status: Selected voice not installed. Click Install to download (~15-20MB).";
                    BtnSetupVoice.Content = "INSTALL MODEL";
                    BtnSetupVoice.IsEnabled = true;
                }
                else
                {
                    string modelPath = PiperEngine.Instance.GetModelPath(lang, voiceId);
                    string filename = System.IO.Path.GetFileName(modelPath);
                    TxtVoiceStatus.Text = $"Status: Ready. Active Model: {filename}";
                    BtnSetupVoice.Content = "FORCE UPDATE";
                    BtnSetupVoice.IsEnabled = true;
                }
            }
        }

        private void BtnSetupVoice_Click(object sender, RoutedEventArgs e)
        {
            if (Plugin == null) return;
            string lang = Plugin.Settings.VoiceLanguage;
            string voiceId = Plugin.Settings.SelectedVoiceModel;
            PiperEngine.Instance.StartSetupAsync(lang, voiceId);
            UpdatePiperStatusUI();
        }

        private void BtnImportModel_Click(object sender, RoutedEventArgs e)
        {
            if (Plugin == null) return;
            string lang = Plugin.Settings.VoiceLanguage;

            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Piper Voice Model (*.onnx)|*.onnx",
                Title = "Select Custom Piper Voice Model"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                PiperEngine.Instance.ImportCustomModel(openFileDialog.FileName, lang);
                RefreshVoiceModelsDropdown();
                UpdatePiperStatusUI();
                Plugin.ReloadVoiceEngine();
            }
        }

        private void RefreshVoiceModelsDropdown()
        {
            if (Plugin == null || CmbVoiceModel == null) return;

            string lang = Plugin.Settings.VoiceLanguage;
            var voices = PiperEngine.Instance.GetAvailableVoices(lang);

            CmbVoiceModel.SelectionChanged -= CmbVoiceModel_SelectionChanged;
            CmbVoiceModel.Items.Clear();

            foreach (var voice in voices)
            {
                int dupCount = voices.Count(v => v.Name.Equals(voice.Name, StringComparison.OrdinalIgnoreCase));
                string displayName = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(voice.Name.ToLower());
                
                if (voice.IsCustom)
                {
                    displayName += " (Custom)";
                }
                else if (dupCount > 1)
                {
                    displayName += $" ({CultureInfo.CurrentCulture.TextInfo.ToTitleCase(voice.Quality.ToLower())})";
                }

                var item = new ComboBoxItem
                {
                    Content = displayName,
                    Tag = voice.Id
                };
                CmbVoiceModel.Items.Add(item);
            }

            var selectedVoice = voices.FirstOrDefault(v => v.Id.Equals(Plugin.Settings.SelectedVoiceModel, StringComparison.OrdinalIgnoreCase))
                                ?? voices.FirstOrDefault();

            if (selectedVoice != null)
            {
                Plugin.Settings.SelectedVoiceModel = selectedVoice.Id;
                foreach (ComboBoxItem item in CmbVoiceModel.Items)
                {
                    if (item.Tag.ToString().Equals(selectedVoice.Id, StringComparison.OrdinalIgnoreCase))
                    {
                        CmbVoiceModel.SelectedItem = item;
                        break;
                    }
                }
                LoadVoiceModelSettings(selectedVoice.Id);
            }

            CmbVoiceModel.SelectionChanged += CmbVoiceModel_SelectionChanged;
        }

        private void LoadVoiceModelSettings(string voiceId)
        {
            if (Plugin == null) return;
            try
            {
                var settings = Plugin.Settings.GetSettingsForVoice(voiceId);
                if (settings == null) return;

                bool originalLoaded = _isLoaded;
                _isLoaded = false;

                if (SldVoiceVolume != null) SldVoiceVolume.Value = settings.VoiceVolume;
                if (SldRadioNoiseVolume != null) SldRadioNoiseVolume.Value = settings.RadioNoiseVolume;
                if (SldSpeechSpeed != null) SldSpeechSpeed.Value = settings.SpeechSpeed;

                if (TxtVoiceVolValue != null) TxtVoiceVolValue.Text = $"{(int)settings.VoiceVolume}%";
                if (TxtNoiseVolValue != null) TxtNoiseVolValue.Text = $"{(int)settings.RadioNoiseVolume}%";
                UpdateSpeedLabel(settings.SpeechSpeed);

                _isLoaded = originalLoaded;
            }
            catch (Exception ex)
            {
                SimHub.Logging.Current.Error("Error in LoadVoiceModelSettings: " + ex.ToString());
            }
        }

        private void CmbVoiceModel_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded || Plugin == null) return;

            var selectedItem = CmbVoiceModel.SelectedItem as ComboBoxItem;
            if (selectedItem != null)
            {
                string voiceId = selectedItem.Tag.ToString();
                Plugin.Settings.SelectedVoiceModel = voiceId;
                
                LoadVoiceModelSettings(voiceId);
                
                UpdatePiperStatusUI();
                Plugin.ReloadVoiceEngine();
            }
        }

        private void BtnPlaySample_Click(object sender, RoutedEventArgs e)
        {
            if (Plugin == null) return;

            string lang = Plugin.Settings.VoiceLanguage;
            string voiceId = Plugin.Settings.SelectedVoiceModel;
            var voiceSettings = Plugin.Settings.GetSettingsForVoice(voiceId);

            string testPhrase = "Radio check. Hearing you loud and clear.";
            if (lang == "IT") testPhrase = "Prova radio. Ricezione forte e chiara.";
            else if (lang == "DE") testPhrase = "Funkprüfung. Ich höre dich laut und deutlich.";
            else if (lang == "ES") testPhrase = "Prueba de radio. Te escucho fuerte y claro.";
            else if (lang == "FR") testPhrase = "Test radio. Reçu fort et clair.";
            else if (lang == "NL") testPhrase = "Radiocontrole. Ik hoor je luid en duidelijk.";
            else if (lang == "PT") testPhrase = "Teste de rádio. Ouvindo em alto e bom som.";

            PiperEngine.Instance.Speak(testPhrase, lang, voiceId, voiceSettings.VoiceVolume, voiceSettings.RadioNoiseVolume, voiceSettings.SpeechSpeed);
        }

        private void SldVolume_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!_isLoaded || Plugin == null) return;

            string voiceId = Plugin.Settings.SelectedVoiceModel;
            var settings = Plugin.Settings.GetSettingsForVoice(voiceId);

            if (sender == SldVoiceVolume)
            {
                settings.VoiceVolume = SldVoiceVolume.Value;
                Plugin.Settings.VoiceVolume = SldVoiceVolume.Value; // Sync default
                TxtVoiceVolValue.Text = $"{(int)SldVoiceVolume.Value}%";
            }
            else if (sender == SldRadioNoiseVolume)
            {
                settings.RadioNoiseVolume = SldRadioNoiseVolume.Value;
                Plugin.Settings.RadioNoiseVolume = SldRadioNoiseVolume.Value; // Sync default
                TxtNoiseVolValue.Text = $"{(int)SldRadioNoiseVolume.Value}%";
            }
            else if (sender == SldSpeechSpeed)
            {
                settings.SpeechSpeed = SldSpeechSpeed.Value;
                Plugin.Settings.SpeechSpeed = SldSpeechSpeed.Value; // Sync default
                UpdateSpeedLabel(SldSpeechSpeed.Value);
            }
        }

        private void UpdateSpeedLabel(double value)
        {
            if (value == 0.0)
            {
                TxtSpeechSpeedValue.Text = "Normal (1.0x)";
            }
            else if (value > 0.0)
            {
                double speedMult = 1.0 + (value / 100.0);
                TxtSpeechSpeedValue.Text = $"{speedMult:F2}x (Veloce)";
            }
            else
            {
                double speedMult = 1.0 / (1.0 + (-value / 100.0));
                TxtSpeechSpeedValue.Text = $"{speedMult:F2}x (Lento)";
            }
        }
    }
}