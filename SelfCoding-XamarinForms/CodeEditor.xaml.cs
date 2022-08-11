using System;
using System.Linq;
using System.Runtime.CompilerServices;
using Xamarin.Essentials;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;

namespace SelfCoding_XamarinForms
{
    public partial class CodeEditor : ContentView
    {
        public static readonly BindableProperty ReferenceLayoutProperty =
            BindableProperty.Create(
                nameof(ReferenceLayout),
                typeof(Layout<View>),
                typeof(CodeEditor));

        public static readonly BindableProperty ControlTypeProperty =
            BindableProperty.Create(
                nameof(ControlType),
                typeof(Type),
                typeof(CodeEditor));

        public static readonly BindableProperty ContextProperty =
            BindableProperty.Create(nameof(Context),
                typeof(OSAppTheme),
                typeof(CodeEditor));

        public static readonly BindableProperty TextProperty =
            BindableProperty.Create(
                nameof(Text),
                typeof(string),
                typeof(CodeEditor));

        private bool internalUpdate;

        public Layout<View> ReferenceLayout
        {
            get => (Layout<View>)GetValue(ReferenceLayoutProperty);
            set => SetValue(ReferenceLayoutProperty, value);
        }

        public Type ControlType
        {
            get => (Type)GetValue(ControlTypeProperty);
            set => SetValue(ControlTypeProperty, value);
        }

        public OSAppTheme Context
        {
            get => (OSAppTheme)GetValue(ContextProperty);
            set => SetValue(ContextProperty, value);
        }

        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        public CodeEditor()
        {
            InitializeComponent();
        }

        protected override void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            base.OnPropertyChanged(propertyName);

            if (nameof(Context).Equals(propertyName))
            {
                UpdateEditorTheme();
            }
            else if (nameof(Text).Equals(propertyName) && !internalUpdate)
            {
                UpdateEditorText();
            }
        }

        private void chartWeb_OnContentLoaded(object sender, System.EventArgs e)
        {
            UpdateEditorSuggestions();
            UpdateEditorText();
            UpdateEditorTheme();

            chartWeb.AddLocalCallback("onEditorCodeChanged",
                (c) => MainThread.BeginInvokeOnMainThread(() => OnEditorCodeChanged(c)));
        }

        private void UpdateEditorSuggestions()
        {
            var properties = string.Join(",", ControlType.GetProperties().Select(p => p.Name));
            var editorJs = $"javascript:setPropertiesForAutocomplete('{properties}');";
            _ = chartWeb.InjectJavascriptAsync(editorJs);
        }

        private void UpdateEditorText()
        {
            var editorJS = $"javascript:setValue('{Text}');";
            _ = chartWeb.InjectJavascriptAsync(editorJS);
        }

        private void UpdateEditorTheme()
        {
            var theme = Context == OSAppTheme.Light ? "vs" : "vs-dark";
            var editorJS = $"javascript:setTheme('{theme}');";
            _ = chartWeb.InjectJavascriptAsync(editorJS);
        }

        private void OnEditorCodeChanged(string xamlCode)
        {
            try
            {
                internalUpdate = true;
                var code = "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
                    "<StackLayout" +
                    "    xmlns=\"http://xamarin.com/schemas/2014/forms\"" +
                    "    xmlns:x=\"http://schemas.microsoft.com/winfx/2009/xaml\"" +
                    "    x:Class=\"SelfCoding_XamarinForms.PlaygroundStack\">"
                    + xamlCode.Trim() +
                    "</StackLayout>";

                Text = code;
                var newControl = Activator.CreateInstance(ControlType) as View;
                newControl = newControl.LoadFromXaml(code);
                newControl.BindingContext = BindingContext;

                ReferenceLayout.Children.Clear();
                ReferenceLayout.Children.Add(newControl);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            finally
            {
                internalUpdate = false;
            }
        }
    }
}