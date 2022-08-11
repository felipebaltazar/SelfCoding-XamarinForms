﻿using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using Xam.Plugin.WebView.Abstractions.Delegates;
using Xam.Plugin.WebView.Abstractions.Enumerations;
using Xam.Plugin.WebView.Abstractions.Models;
using Xamarin.Forms;

[assembly: InternalsVisibleTo("Xam.Plugin.WebView.UWP")]
[assembly: InternalsVisibleTo("Xam.Plugin.WebView.Droid")]
[assembly: InternalsVisibleTo("Xam.Plugin.WebView.iOS")]
[assembly: InternalsVisibleTo("Xam.Plugin.WebView.MacOS")]
namespace Xam.Plugin.WebView.Abstractions
{
    public partial class FormsWebView : View, IFormsWebView, IDisposable
    {

        /// <summary>
        /// A delegate which takes valid javascript and returns the response from it, if the response is a string.
        /// </summary>
        /// <param name="js">The valid JS to inject</param>
        /// <returns>Any string response from the DOM or string.Empty</returns>
        public delegate Task<string> JavascriptInjectionRequestDelegate(string js);


        /// <summary>
        /// Delegate to await clearing cookies. Will remove all temporary data on UWP
        /// </summary>
        public delegate Task ClearCookiesRequestDelegate();

        /// <summary>
        /// Fired when navigation begins, for example when the source is set.
        /// </summary>
        public event EventHandler<DecisionHandlerDelegate> OnNavigationStarted;

        /// <summary>
        /// Fires when navigation is completed. This can be either as the result of a valid navigation, or on an error.
        /// Returns the URL of the page navigated to.
        /// </summary>
        public event EventHandler<string> OnNavigationCompleted;

        /// <summary>
        /// Fires when navigation fires an error. By default this uses the native systems error codes.
        /// </summary>
        public event EventHandler<int> OnNavigationError;

        /// <summary>
        /// Fires when the content on the DOM is ready. All your calls to Javascript using C# should be performed after this is fired.
        /// </summary>
        public event EventHandler OnContentLoaded;

        public event EventHandler OnBackRequested;

        public event EventHandler OnForwardRequested;

        public event EventHandler OnRefreshRequested;

        public event JavascriptInjectionRequestDelegate OnJavascriptInjectionRequest;
        public event JavascriptInjectionRequestDelegate OnJavascriptInjection;
        public event ClearCookiesRequestDelegate OnClearCookiesRequested;

        public ConcurrentDictionary<string, Action<string>> LocalRegisteredCallbacks { get; }

        public bool WaitForJsResponse { get; set; }

        private object _disposedlock = new object();

        /// <summary>
        /// A dictionary containing all headers to be injected into the request. Local headers take precedence over global ones.
        /// </summary>
        public readonly ConcurrentDictionary<string, string> LocalRegisteredHeaders = new ConcurrentDictionary<string, string>();

        /// <summary>
        /// The content type to attempt to load. By default this is Internet.
        /// </summary>
        public WebViewContentType ContentType
        {
            get => (WebViewContentType)GetValue(ContentTypeProperty);
            set => SetValue(ContentTypeProperty, value);
        }


        public ICommand OnContentLoadedCommand
        {
            get => (ICommand)GetValue(OnContentLoadedCommandProperty);
            set => SetValue(OnContentLoadedCommandProperty, value);
        }

        /// <summary>
        /// The source data. This can either be a valid URL, a path to a local file, or a HTML string.
        /// </summary>
        public string Source
        {
            get => (string)GetValue(SourceProperty);
            set => SetValue(SourceProperty, value);
        }

        /// <summary>
        /// Indicate render mode
        /// </summary>
        public WebViewRenderMode RenderMode
        {
            get => (WebViewRenderMode)GetValue(RenderModeProperty);
            set => SetValue(RenderModeProperty, value);
        }

        /// <summary>
        /// The current url.
        /// </summary>
        public string CurrentUrl
        {
            get => (string)GetValue(CurrentUrlProperty);
            set => SetValue(CurrentUrlProperty, value);
        }

        /// <summary>
        /// Override the BaseURL in the renderer with this property.
        /// By default, the BaseUrls are the following:
        /// Android) Assets folder with AndroidAsset build property
        /// iOS and MacOS) Resources folder with BundleResource build property
        /// UWP) Project folder with content build property
        /// </summary>
        public string BaseUrl
        {
            get { return (string)GetValue(BaseUrlProperty); }
            set { SetValue(BaseUrlProperty, value); }
        }

        /// <summary>
        /// Opt in and out of global callbacks
        /// </summary>
        public bool EnableGlobalCallbacks
        {
            get => (bool)GetValue(EnableGlobalCallbacksProperty);
            set => SetValue(EnableGlobalCallbacksProperty, value);
        }

        /// <summary>
        /// Opt in and out of global headers
        /// </summary>
        public bool EnableGlobalHeaders
        {
            get => (bool)GetValue(EnableGlobalHeadersProperty);
            set => SetValue(EnableGlobalHeadersProperty, value);
        }

        /// <summary>
        /// Bindable property which is true when the page is currently navigating.
        /// </summary>
        public bool Navigating
        {
            get => (bool)GetValue(NavigatingProperty);
            set => SetValue(NavigatingProperty, value);
        }

        /// <summary>
        /// Bindable property which is true when the webview can go back a page.
        /// </summary>
        public bool CanGoBack
        {
            get => (bool)GetValue(CanGoBackProperty);
            set => SetValue(CanGoBackProperty, value);
        }

        /// <summary>
        /// Bindable property which is true when the webview can go forward a page.
        /// </summary>
        public bool CanGoForward
        {
            get => (bool)GetValue(CanGoForwardProperty);
            set => SetValue(CanGoForwardProperty, value);
        }

        public bool UseWideViewPort
        {
            get => (bool)GetValue(UseWideViewPortProperty);
            set => SetValue(UseWideViewPortProperty, value);
        }

        public FormsWebView()
        {
            HorizontalOptions = VerticalOptions = LayoutOptions.FillAndExpand;
            LocalRegisteredCallbacks = new ConcurrentDictionary<string, Action<string>>();
        }

        /// <summary>
        /// Navigate back a page if capable of doing so.
        /// </summary>
        public void GoBack()
        {
            if (!CanGoBack) return;
            OnBackRequested?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Navigate forward a page if capable of doing so.
        /// </summary>
        public void GoForward()
        {
            if (!CanGoForward) return;
            OnForwardRequested?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Refresh the current page if capable of doing so.
        /// </summary>
        public void Refresh()
        {
            OnRefreshRequested?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Clearing all cookies.
        /// For UWP, all temporary browser data will be cleared.
        /// </summary>
        public async Task ClearCookiesAsync()
        {
            if (OnClearCookiesRequested != null)
                await OnClearCookiesRequested.Invoke();
        }

        /// <summary>
        /// Inject some javascript, returning a string result if the resulting Javascript resolves to a string on the DOM.
        /// For example 'document.body.style.backgroundColor = \"red\";' will return 'red'.
        /// </summary>
        /// <param name="js">The javascript to inject</param>
        /// <returns>A valid string response or string.Empty</returns>
        public async Task<string> InjectJavascriptAsync(string js)
        {
            if (string.IsNullOrWhiteSpace(js))
                return string.Empty;

            if (OnJavascriptInjectionRequest != null)
                return await OnJavascriptInjectionRequest?.Invoke(js);

            return string.Empty;
        }

        /// <summary>
        /// Inject some javascript, returning a string result if the resulting Javascript resolves to a string on the DOM.
        /// For example 'document.body.style.backgroundColor = \"red\";' will return 'red'.
        /// </summary>
        /// <param name="js">The javascript to inject</param>
        /// <returns>A valid string response or string.Empty</returns>
        public async Task<string> InjectJavascript(string js)
        {
            if (string.IsNullOrWhiteSpace(js))
                return string.Empty;

            if (OnJavascriptInjectionRequest != null)
                return await OnJavascriptInjection?.Invoke(js);

            return string.Empty;
        }

        /// <summary>
        /// Adds a callback to the DOM, this callback when passed a string by the Javascript, will fire an action with that string as the parameter.
        /// </summary>
        /// <param name="functionName">The name of the function</param>
        /// <param name="action">The action to call back to</param>
        public void AddLocalCallback(string functionName, Action<string> action)
        {
            if (string.IsNullOrWhiteSpace(functionName)) return;

            if (LocalRegisteredCallbacks.ContainsKey(functionName))
                LocalRegisteredCallbacks.TryRemove(functionName, out Action<string> act);

            LocalRegisteredCallbacks.TryAdd(functionName, action);
            _weakEventManager.RaiseEvent(this, functionName, nameof(CallbackAdded));
        }

        /// <summary>
        /// Removes a callback by the function name.
        /// Note: this does not remove it from the DOM, rather it removes the action, resulting in your view never getting the response.
        /// </summary>
        /// <param name="functionName"></param>
        public void RemoveLocalCallback(string functionName)
        {
            if (LocalRegisteredCallbacks.ContainsKey(functionName))
                LocalRegisteredCallbacks.TryRemove(functionName, out Action<string> act);
        }

        /// <summary>
        /// Removes all local callbacks from the DOM.
        /// Note: this does not remove it from the DOM, rather it removes the action, resulting in your view never getting the response.
        /// </summary>
        public void RemoveAllLocalCallbacks()
        {
            LocalRegisteredCallbacks.Clear();
        }

        /// <summary>
        /// Dispose of the WebView
        /// </summary>
        public void Dispose()
        {
            LocalRegisteredCallbacks.Clear();
            LocalRegisteredHeaders.Clear();

            ClearOnJavascriptInjectionEvent();
            ClearOnClearCookiesRequestedEvent();
            ClearOnJavascriptInjectionRequestEvent();
        }

        private void ClearOnJavascriptInjectionEvent()
        {
            try
            {

                if (OnJavascriptInjection?.GetInvocationList().Length > 0)
                {
                    var subscribers = OnJavascriptInjection.GetInvocationList();
                    var subscriberCount = subscribers.Length;

                    for (int i = 0; i < subscriberCount; i++)
                    {
                        OnJavascriptInjection -= subscribers[i] as JavascriptInjectionRequestDelegate;
                    }
                }
            }
            catch (Exception ex)
            {
                //Esse trecho não será compilado em Release
#if DEBUG
                Console.WriteLine(ex);
                Debugger.Break();
#endif
            }
        }

        private void ClearOnJavascriptInjectionRequestEvent()
        {
            try
            {
                if (OnJavascriptInjectionRequest?.GetInvocationList().Length > 0)
                {
                    var subscribers = OnJavascriptInjectionRequest.GetInvocationList();
                    var subscriberCount = subscribers.Length;

                    for (int i = 0; i < subscriberCount; i++)
                    {
                        OnJavascriptInjectionRequest -= subscribers[i] as JavascriptInjectionRequestDelegate;
                    }
                }
            }
            catch (Exception ex)
            {
                //Esse trecho não será compilado em Release
#if DEBUG
                Console.WriteLine(ex);
                Debugger.Break();
#endif
            }
        }

        private void ClearOnClearCookiesRequestedEvent()
        {
            try
            {
                if (OnClearCookiesRequested?.GetInvocationList().Length > 0)
                {
                    var subscribers = OnClearCookiesRequested.GetInvocationList();
                    var subscriberCount = subscribers.Length;

                    for (int i = 0; i < subscriberCount; i++)
                    {
                        OnClearCookiesRequested -= subscribers[i] as ClearCookiesRequestDelegate;
                    }
                }
            }
            catch (Exception ex)
            {
            //Esse trecho não será compilado em Release
#if DEBUG
                Console.WriteLine(ex);
                Debugger.Break();
#endif
            }
        }

        // All code which should be hidden from the end user goes here
        #region Internals

        public DecisionHandlerDelegate HandleNavigationStartRequest(string uri)
        {
            // By default, we only attempt to offload valid Uris with none http/s schemes
            bool validUri = Uri.TryCreate(uri, UriKind.Absolute, out Uri uriResult);
            bool validScheme = false;

            if (validUri)
                validScheme = uriResult.Scheme.StartsWith("http", StringComparison.OrdinalIgnoreCase) || uriResult.Scheme.StartsWith("file", StringComparison.OrdinalIgnoreCase);

            var handler = new DecisionHandlerDelegate()
            {
                Uri = uri,
                OffloadOntoDevice = validUri && !validScheme
            };

            OnNavigationStarted?.Invoke(this, handler);
            return handler;
        }

        public void HandleNavigationCompleted(string uri)
        {
            OnNavigationCompleted?.Invoke(this, uri);
        }

        public void HandleNavigationError(int errorCode)
        {
            OnNavigationError?.Invoke(this, errorCode);
        }

        public void HandleContentLoaded()
        {
            OnContentLoadedCommand?.Execute(this.BindingContext);
            OnContentLoaded?.Invoke(this, EventArgs.Empty);
        }

        public void HandleScriptReceived(string data)
        {
            if (string.IsNullOrWhiteSpace(data)) return;

            try
            {

                var action = JsonConvert.DeserializeObject<ActionEvent>(data);

                // Decode
                byte[] dBytes = Convert.FromBase64String(action.Data);
                action.Data = Encoding.UTF8.GetString(dBytes, 0, dBytes.Length);

                // Local takes priority
                if (LocalRegisteredCallbacks.ContainsKey(action.Action))
                    LocalRegisteredCallbacks[action.Action]?.Invoke(action.Data);

                // Global is checked if local fails
                else if (GlobalRegisteredCallbacks.ContainsKey(action.Action))
                    GlobalRegisteredCallbacks[action.Action]?.Invoke(action.Data);
            }
            catch (Exception ex)
            {
                //Esse trecho não será compilado em Release
#if DEBUG
                Console.WriteLine(ex);
                Debugger.Break();
#endif
            }
        }

        #endregion
    }

    public enum WebViewRenderMode
    {
        /// Render HTML Page
        Html = 0,
        /// Render PDF Viewer
        Pdf = 1
    }
}
