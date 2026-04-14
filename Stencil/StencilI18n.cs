using System;
using System.Collections.Generic;
using System.Globalization;

namespace Stencil
{
    public static class StencilI18n
    {
        private static readonly Dictionary<string, string> English = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "window.title", "Stencil" },
            { "window.chooseModel", "Choose Model File" },
            { "label.importFormats", "Import formats: .stl, .obj" },
            { "label.tipPutFiles", "After placing model files in the folder, click Refresh." },
            { "label.noFileSelected", "No file selected." },
            { "label.models", "Models:" },
            { "label.folder", "Folder: {0}" },
            { "label.noSupportedFiles", "No .stl/.obj files found." },
            { "label.selectModelFirst", "Select a model first." },
            { "label.status", "Status: {0}" },
            { "button.chooseFile", "Choose File" },
            { "button.import", "Import" },
            { "button.removeSelected", "Remove Selected" },
            { "button.removeAll", "Remove All" },
            { "button.refresh", "Refresh" },
            { "button.close", "Close" },
            { "status.selected", "Selected: {0}" },
            { "status.noFilesFound", "No model files found in: {0}" },
            { "status.filesFound", "Found {0} file(s)." },
            { "status.invalidColor", "Invalid color. Use #RRGGBB or #RRGGBBAA." }
        };

        private static readonly Dictionary<string, string> Chinese = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "window.title", "Stencil" },
            { "window.chooseModel", "选择模型文件" },
            { "label.importFormats", "支持导入格式：.stl, .obj" },
            { "label.tipPutFiles", "将模型放入文件夹后请点击 刷新" },
            { "label.noFileSelected", "未选择文件。" },
            { "label.models", "模型列表：" },
            { "label.folder", "目录：{0}" },
            { "label.noSupportedFiles", "未找到 .stl/.obj 文件。" },
            { "label.selectModelFirst", "请先选择一个模型。" },
            { "label.status", "状态：{0}" },
            { "button.chooseFile", "选择文件" },
            { "button.import", "导入" },
            { "button.removeSelected", "删除选中" },
            { "button.removeAll", "全部删除" },
            { "button.refresh", "刷新" },
            { "button.close", "关闭" },
            { "status.selected", "已选择：{0}" },
            { "status.noFilesFound", "在以下目录未找到模型文件：{0}" },
            { "status.filesFound", "找到 {0} 个文件。" },
            { "status.invalidColor", "颜色格式无效。请使用 #RRGGBB 或 #RRGGBBAA。" }
        };

        public static string Tr(string key, params object[] args)
        {
            string text;
            if (!CurrentMap().TryGetValue(key, out text))
            {
                if (!English.TryGetValue(key, out text))
                {
                    text = key;
                }
            }

            if (args == null || args.Length == 0)
            {
                return text;
            }

            return string.Format(CultureInfo.CurrentCulture, text, args);
        }

        private static Dictionary<string, string> CurrentMap()
        {
            if (IsChineseLanguage())
            {
                return Chinese;
            }

            return English;
        }

        private static bool IsChineseLanguage()
        {
            // Only use KSP game localization.
            try
            {
                var localizerType = Type.GetType("KSP.Localization.Localizer, Assembly-CSharp") ??
                                   Type.GetType("Localizer, Assembly-CSharp");
                if (localizerType != null)
                {
                    var currentLanguageProp = localizerType.GetProperty("CurrentLanguage");
                    if (currentLanguageProp != null)
                    {
                        var lang = currentLanguageProp.GetValue(null, null);
                        if (lang != null)
                        {
                            var langText = lang.ToString();
                            if (!string.IsNullOrWhiteSpace(langText) && langText.IndexOf("zh", StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                return true;
                            }
                        }
                    }
                }
            }
            catch
            {
            }

            return false;
        }
    }
}
