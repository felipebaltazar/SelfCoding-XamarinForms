using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Android.Content;
using Android.OS;
using Android.Views;
using Android.Webkit;
using AsyncAwaitBestPractices;
using Xam.Plugin.WebView.Abstractions;
using Xam.Plugin.WebView.Abstractions.Enumerations;
using Xam.Plugin.WebView.Droid;
using Xamarin.Essentials;
using Xamarin.Forms;
using Xamarin.Forms.Platform.Android;
using static Xam.Plugin.WebView.Abstractions.FormsWebView;
using AWebView = Android.Webkit.WebView;

[assembly: ExportRenderer(typeof(FormsWebView), typeof(FormsWebViewRenderer))]
namespace Xam.Plugin.WebView.Droid
{
    public sealed class FormsWebViewRenderer : ViewRenderer<FormsWebView, Android.Webkit.WebView>
    {
        private const string MimeType = "text/html";
        private const string EncodingType = "UTF-8";
        private const string HistoryUri = "";
        private const string NULL_JS = "null";
        private const string TRUE_JS = "true";

        private readonly static WeakEventManager<AWebView> _weakEventManager = new WeakEventManager<AWebView>();

        private readonly Context _context;

        private readonly WeakReference<FormsWebViewBridge> _bridge;
        private readonly WeakReference<WebViewClient> _webViewClient;
        private readonly WeakReference<WebChromeClient> _webViewChromeClient;

        private readonly Regex _unicodeRegex =
            new Regex(@"\\[Uu]([0-9A-Fa-f]{4})", RegexOptions.Compiled);

        private readonly WeakReference<LayoutParams> _layoutParams =
            new WeakReference<LayoutParams>(new LayoutParams(LayoutParams.MatchParent, LayoutParams.MatchParent));

        private WeakReference<JavascriptValueCallback> callback;
        private WeakReference<AWebView> webView;

        private bool _disposed;
        private bool _isDisposed = false;

        public static string BaseUrl { get; set; } = "file:///android_asset/";

        public static bool IgnoreSSLGlobally { get; set; }

        public static event EventHandler<AWebView> OnControlChanged
        {
            add => _weakEventManager.AddEventHandler(value, nameof(OnControlChanged));
            remove => _weakEventManager.RemoveEventHandler(value, nameof(OnControlChanged));
        }

        public FormsWebViewRenderer(Context context) : base(context)
        {
            _context = context;
            _bridge = new WeakReference<FormsWebViewBridge>(new FormsWebViewBridge(this));
            _webViewClient = new WeakReference<WebViewClient>(new FormsWebViewClient(this));
            _webViewChromeClient = new WeakReference<WebChromeClient>(new FormsWebViewChromeClient(this));
        }

        protected override void OnElementChanged(ElementChangedEventArgs<FormsWebView> e)
        {
            try
            {
                if (this == null)
                    return;

                base.OnElementChanged(e);

                if (Control is null && Element != null)
                    SetupControl();

                if (e.NewElement != null)
                    SetupElement(e.NewElement);

                if (e.OldElement != null)
                    DestroyElement(e.OldElement);

                if (Element.UseWideViewPort)
                {
                    Control.Settings.LoadWithOverviewMode = true;
                    Control.Settings.UseWideViewPort = true;
                }
            }
            catch (Exception ex)
            {
                Console.Write(ex);
            }
        }

        private void SetupElement(FormsWebView element)
        {
            _disposed = false;
            element.PropertyChanged += OnPropertyChanged;
            element.OnJavascriptInjectionRequest += OnJavascriptInjectionRequest;
            element.OnJavascriptInjection += OnJavascriptInjection;
            element.OnClearCookiesRequested += OnClearCookiesRequest;
            element.OnBackRequested += OnBackRequested;
            element.OnForwardRequested += OnForwardRequested;
            element.OnRefreshRequested += OnRefreshRequested;
            element.OnNavigationCompleted += Element_OnNavigationCompleted;
            if (element.RenderMode == WebViewRenderMode.Pdf)
            {
                Control.Settings.AllowFileAccess = true;
                Control.Settings.AllowFileAccessFromFileURLs = true;
                Control.Settings.AllowUniversalAccessFromFileURLs = true;
            }
            SetSource();
        }

        private void DestroyElement(FormsWebView element)
        {
            _disposed = true;
            UnsubscribeEvents(element);
            element.Dispose();
        }

        private void UnsubscribeEvents(FormsWebView element)
        {
            element.PropertyChanged -= OnPropertyChanged;
            element.OnJavascriptInjectionRequest -= OnJavascriptInjectionRequest;
            element.OnJavascriptInjection -= OnJavascriptInjection;
            element.OnClearCookiesRequested -= OnClearCookiesRequest;
            element.OnBackRequested -= OnBackRequested;
            element.OnForwardRequested -= OnForwardRequested;
            element.OnRefreshRequested -= OnRefreshRequested;
        }

        private void SetupControl()
        {
            if (webView != null)
            {
                if (webView.TryGetTarget(out var oldWebView))
                {
                    if (oldWebView != null)
                        return;
                }
            }

            var androiWebView = new AWebView(_context);
            webView = new WeakReference<AWebView>(androiWebView);

            ResetCallback();

            if (!_layoutParams.TryGetTarget(out var layoutParams))
                return;

            // https://github.com/SKLn-Rad/Xam.Plugin.WebView.Webview/issues/11
            androiWebView.LayoutParameters = layoutParams;

            // Defaults
            androiWebView.Settings.JavaScriptEnabled = true;
            androiWebView.Settings.DomStorageEnabled = true;

            if (_bridge.TryGetTarget(out var formsWebViewBridge))
                androiWebView.AddJavascriptInterface(formsWebViewBridge, "bridge");

            if (_webViewClient.TryGetTarget(out var webViewClient))
                androiWebView.SetWebViewClient(webViewClient);

            if (_webViewChromeClient.TryGetTarget(out var webViewChromeClient))
                androiWebView.SetWebChromeClient(webViewChromeClient);

            androiWebView.SetBackgroundColor(Android.Graphics.Color.Transparent);

            CallbackAdded -= OnCallbackAdded;
            CallbackAdded += OnCallbackAdded;

            SetNativeControl(androiWebView);
            _weakEventManager.RaiseEvent(this, androiWebView, nameof(OnControlChanged));
        }

        private void OnCallbackAdded(object sender, string e)
        {
            if (Element == null || string.IsNullOrWhiteSpace(e)) return;

            if ((sender == null && Element.EnableGlobalCallbacks) || sender != null)
                JavascriptInjectionRequest(GenerateFunctionScript(e));
        }

        private void OnForwardRequested(object sender, EventArgs e)
        {
            if (Control is null) return;

            if (Control.CanGoForward())
                Control.GoForward();
        }

        private void OnBackRequested(object sender, EventArgs e)
        {
            if (Control is null) return;

            if (Control.CanGoBack())
                Control.GoBack();
        }

        private void Element_OnNavigationCompleted(object sender, string e)
        {
            if (Element != null)
                Element.CurrentUrl = Control.Url;
        }

        private void OnRefreshRequested(object sender, EventArgs e)
        {
            try
            {
                if (Control is null) return;
                Control.Reload();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        private void OnPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case "Source":
                    SetSource();
                    break;
            }
        }

        private Task OnClearCookiesRequest()
        {
            if (Control is null) return Task.CompletedTask;

            if (Build.VERSION.SdkInt >= BuildVersionCodes.LollipopMr1)
            {
                CookieManager.Instance.RemoveAllCookies(null);
                CookieManager.Instance.Flush();
            }
            else
            {
#pragma warning disable CS0618 // O tipo ou membro é obsoleto
                using (var cookieSyncMngr = CookieSyncManager.CreateInstance(_context))
#pragma warning restore CS0618
                {
                    cookieSyncMngr.StartSync();

                    var cookieManager = CookieManager.Instance;
                    cookieManager.RemoveAllCookie();
                    cookieManager.RemoveSessionCookie();

                    cookieSyncMngr.StopSync();
                    cookieSyncMngr.Sync();
                }
            }

            return Task.CompletedTask;
        }

        private const int MAX_ATTEMPTS = 3;
        internal async Task<string> OnJavascriptInjectionRequest(string js)
        {
            var response = string.Empty;

            if (_disposed || Element == null || Control == null || callback == null)
                return response;

            if (!callback.TryGetTarget(out _))
                ResetCallback();

            if (!callback.TryGetTarget(out var jsCallback))
                return response;

            // fire!
            try
            {
                ResetAndInvokeJs(js, jsCallback);

                // wait!
                await Task.Run(async () =>
                {
                    var attempt = 0;
                    while (jsCallback.Value == null && attempt < MAX_ATTEMPTS)
                    {
                        await Task.Delay(TimeSpan.FromMilliseconds(600)).ConfigureAwait(false);
                        attempt++;
                    }

                    // Get the string and strip off the quotes
                    if (jsCallback.Value is Java.Lang.String)
                    {
                        // Unescape that damn Unicode Java bull.
                        response = _unicodeRegex.Replace(jsCallback.Value.ToString(), m => char.ToString((char)ushort.Parse(m.Groups[1].Value, NumberStyles.AllowHexSpecifier)));
                        response = Regex.Unescape(response);

                        if (response.Equals("\"null\""))
                            response = null;

                        else if (response.StartsWith("\"") && response.EndsWith("\""))
                            response = response.Substring(1, response.Length - 2);
                    }

                });
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }

            // return
            return response;
        }

        internal void JavascriptInjectionRequest(string js)
        {
            if (_disposed || Element == null || Control == null || callback == null)
                return;

            if (!callback.TryGetTarget(out _))
                ResetCallback();

            if (!callback.TryGetTarget(out var jsCallback))
                return;

            // fire and forget!
            Task.Run(() =>
            {
                try
                {
                    ResetAndInvokeJs(js, jsCallback);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }
            });
        }

        private async Task<string> OnJavascriptInjection(string js)
        {
            var response = string.Empty;

            if (_disposed || Element == null || Control == null || callback == null)
                return response;

            if (!callback.TryGetTarget(out _))
                ResetCallback();

            if (!callback.TryGetTarget(out var jsCallback))
                return response;

            //Hack para manter o comportamento atual do webview no android
            if (!Element.WaitForJsResponse)
            {
                ResetAndInvokeJs(js, jsCallback);
                return response;
            }

            const int timeoutMs = 1500;
            jsCallback.TaskCallback = new TaskCompletionSource<string>();

            using (var ct = new CancellationTokenSource(timeoutMs))
            {
                try
                {
                    ct.Token.Register(() => jsCallback.TaskCallback?.TrySetCanceled(), useSynchronizationContext: false);

                    // fire!
                    ResetAndInvokeJs(js, jsCallback);
                    if (jsCallback.TaskCallback != null)
                        response = await jsCallback.TaskCallback.Task.ConfigureAwait(false);

                    // return
                    return response == NULL_JS ? TRUE_JS : response;
                }
                catch (TaskCanceledException)
                {
                    return TRUE_JS;
                }
            }
        }

        private void ResetAndInvokeJs(string js, JavascriptValueCallback jsCallback)
        {
            try
            {
                jsCallback.Reset();

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    try
                    {
                        if (Control != null)
                            Control.EvaluateJavascript(js, jsCallback);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex);
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        internal void SetSource()
        {
            if (Element == null || Control is null || string.IsNullOrWhiteSpace(Element.Source)) return;

            switch (Element.ContentType)
            {
                case WebViewContentType.Internet:
                    LoadFromInternet();
                    break;

                case WebViewContentType.LocalFile:
                    LoadFromFile();
                    break;

                case WebViewContentType.StringData:
                    LoadFromString();
                    break;
            }
        }

        private void LoadFromString()
        {
            if (Element == null || Control is null || Element.Source == null) return;

            // Check cancellation
            var handler = Element.HandleNavigationStartRequest(Element.Source);
            if (handler.Cancel) return;

            // Load
            Control.LoadDataWithBaseURL(Element.BaseUrl ?? BaseUrl, Element.Source, MimeType, EncodingType, HistoryUri);
        }

        private void LoadFromFile()
        {
            if (Element == null || Control is null || Element.Source == null) return;

            Control.LoadUrl(Path.Combine(Element.BaseUrl ?? BaseUrl, Element.Source));
        }

        private void LoadFromInternet()
        {
            if (Element == null || Control is null || Element.Source == null) return;

            var headers = new Dictionary<string, string>();

            // Add Local Headers
            foreach (var header in Element.LocalRegisteredHeaders)
            {
                if (!headers.ContainsKey(header.Key))
                    headers.Add(header.Key, header.Value);
            }

            // Add Global Headers
            if (Element.EnableGlobalHeaders)
            {
                foreach (var header in FormsWebView.GlobalRegisteredHeaders)
                {
                    if (!headers.ContainsKey(header.Key))
                        headers.Add(header.Key, header.Value);
                }
            }

            Control.LoadUrl(Element.Source, headers);
        }

        protected override void Dispose(bool disposing)
        {
            // HACK to work around an issue in WebViewRenderer where Dispose does not clear an event handler
            if (disposing && Element != null)
            {
                // we happen to know the name of the private method used for the handler - this will break if the name changes
                var handler = (JavascriptInjectionRequestDelegate)Delegate.CreateDelegate(typeof(JavascriptInjectionRequestDelegate), this, "OnJavascriptInjectionRequest");
                Element.OnJavascriptInjectionRequest -= handler;
            }

            UnsubscribeEvents(Element);
            Element.Dispose();
            RemoveWebViewClients();
            base.Dispose(disposing);

            //_bridge.TryDisposeReference();
            //_webViewClient.TryDisposeReference();
            //_webViewChromeClient.TryDisposeReference();
            //_layoutParams.TryDisposeReference();
            //callback.TryDisposeReference();

            _isDisposed = true;
        }

        //HACK: Se nao remover as referencias do webview antes do dispose causa memoryleak
        private void RemoveWebViewClients()
        {
            if (webView == null)
                return;

            if (!webView.TryGetTarget(out var androiWebView))
                return;

            if (androiWebView is null)
                return;

            try
            {
                androiWebView.RemoveJavascriptInterface("bridge");
                androiWebView.SetWebChromeClient(null);
                androiWebView.SetWebViewClient(null);

                ((ViewGroup)androiWebView.Parent).RemoveView(androiWebView);
                androiWebView.RemoveAllViews();
                androiWebView.Destroy();
                androiWebView = null;
            }
            catch { }
        }

        private void ResetCallback()
        {
            //callback.TryDisposeReference();
            callback = new WeakReference<JavascriptValueCallback>(new JavascriptValueCallback(this));
        }
    }
}