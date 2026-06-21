namespace LogViewer.Properties {
    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("Microsoft.VisualStudio.Editors.SettingsDesigner.SettingsSingleFileGenerator", "17.0.0.0")]
    internal sealed partial class Settings : global::System.Configuration.ApplicationSettingsBase {
        private static Settings defaultInstance = ((Settings)(global::System.Configuration.ApplicationSettingsBase.Synchronized(new Settings())));
        public static Settings Default {
            get { return defaultInstance; }
        }

        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("9527")]
        public int ServerPort { get { return ((int)(this["ServerPort"])); } set { this["ServerPort"] = value; } }

        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("5000")]
        public int MaxLogEntriesPerDevice { get { return ((int)(this["MaxLogEntriesPerDevice"])); } set { this["MaxLogEntriesPerDevice"] = value; } }

        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("10000")]
        public int MaxLogEntriesAll { get { return ((int)(this["MaxLogEntriesAll"])); } set { this["MaxLogEntriesAll"] = value; } }

        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("10000")]
        public int MaxSystemLogEntries { get { return ((int)(this["MaxSystemLogEntries"])); } set { this["MaxSystemLogEntries"] = value; } }

        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("1000")]
        public int AndroidQueueSize { get { return ((int)(this["AndroidQueueSize"])); } set { this["AndroidQueueSize"] = value; } }

        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("50")]
        public int MaxBodySizeKb { get { return ((int)(this["MaxBodySizeKb"])); } set { this["MaxBodySizeKb"] = value; } }

        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("True")]
        public bool AutoAdbReverse { get { return ((bool)(this["AutoAdbReverse"])); } set { this["AutoAdbReverse"] = value; } }

        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("True")]
        public bool AutoStartLogcat { get { return ((bool)(this["AutoStartLogcat"])); } set { this["AutoStartLogcat"] = value; } }

        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("True")]
        public bool AutoFormatJson { get { return ((bool)(this["AutoFormatJson"])); } set { this["AutoFormatJson"] = value; } }

        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("11")]
        public int FontSize { get { return ((int)(this["FontSize"])); } set { this["FontSize"] = value; } }

        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("2000")]
        public int AdbScanIntervalMs { get { return ((int)(this["AdbScanIntervalMs"])); } set { this["AdbScanIntervalMs"] = value; } }

        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("")]
        public string LogcatFilter { get { return ((string)(this["LogcatFilter"])); } set { this["LogcatFilter"] = value; } }
    [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("")]
        public string AdbPath { get { return ((string)(this["AdbPath"])); } set { this["AdbPath"] = value; } }

        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("")]
        public string ScrcpyPath { get { return ((string)(this["ScrcpyPath"])); } set { this["ScrcpyPath"] = value; } }

        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("False")]
        public bool AutoStartScrcpyForSelectedDevice { get { return ((bool)(this["AutoStartScrcpyForSelectedDevice"])); } set { this["AutoStartScrcpyForSelectedDevice"] = value; } }

        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("340")]
        public int LastLeftPanelWidth { get { return ((int)(this["LastLeftPanelWidth"])); } set { this["LastLeftPanelWidth"] = value; } }
    }
}
