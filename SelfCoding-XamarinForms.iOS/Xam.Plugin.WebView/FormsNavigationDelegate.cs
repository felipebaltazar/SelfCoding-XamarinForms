using System;
using Foundation;
using WebKit;
using Xam.Plugin.WebView.Abstractions;
using UIKit;

namespace Xam.Plugin.WebView.iOS
{
    public class FormsNavigationDelegate : WKNavigationDelegate
    {

        readonly WeakReference<FormsWebViewRenderer> Reference;

        public FormsNavigationDelegate(FormsWebViewRenderer renderer)
        {
            Reference = new WeakReference<FormsWebViewRenderer>(renderer);
        }

        public bool AttemptOpenCustomUrlScheme(NSUrl url)
        {
            var app = UIApplication.SharedApplication;

            if (app.CanOpenUrl(url))
                return app.OpenUrl(url);

            return false;
        }

        [Export("webView:decidePolicyForNavigationAction:decisionHandler:")]
        public override void DecidePolicy(WKWebView webView, WKNavigationAction navigationAction, Action<WKNavigationActionPolicy> decisionHandler)
        {
            if (Reference == null || !Reference.TryGetTarget(out FormsWebViewRenderer renderer)) return;
            if (renderer.Element == null) return;

            var response = renderer.Element.HandleNavigationStartRequest(navigationAction.Request.Url.ToString());

            if (response.Cancel || response.OffloadOntoDevice)
            {
                if (response.OffloadOntoDevice)
                    AttemptOpenCustomUrlScheme(navigationAction.Request.Url);

                decisionHandler(WKNavigationActionPolicy.Cancel);
            }

            else
            {
                decisionHandler(WKNavigationActionPolicy.Allow);
                renderer.Element.Navigating = true;
            }
        }
        
        [Export("webView:decidePolicyForNavigationResponse:decisionHandler:")]
        public override void DecidePolicy(WKWebView webView, WKNavigationResponse navigationResponse, Action<WKNavigationResponsePolicy> decisionHandler)
        {
            //HACK ajuste para evitar o crash de
            //[Xam_Plugin_WebView_iOS_FormsNavigationDelegate webView:decidePolicyForNavigationResponse:decisionHandler:] was not called
            if (Reference == null || !Reference.TryGetTarget(out FormsWebViewRenderer renderer) || renderer.Element == null) {
                decisionHandler(WKNavigationResponsePolicy.Allow);
                return;
            }

            if (navigationResponse.Response is NSHttpUrlResponse)
            {
                var code = ((NSHttpUrlResponse)navigationResponse.Response).StatusCode;
                if (code >= 400)
                {
                    renderer.Element.Navigating = false;
                    renderer.Element.HandleNavigationError((int)code);
                    decisionHandler(WKNavigationResponsePolicy.Cancel);
                    return;
                }
            }

            decisionHandler(WKNavigationResponsePolicy.Allow);
        }

        [Export("webView:didFinishNavigation:")]
        public async override void DidFinishNavigation(WKWebView webView, WKNavigation navigation)
        {
            if (Reference == null || !Reference.TryGetTarget(out FormsWebViewRenderer renderer)) return;
            if (renderer.Element == null) return;

            renderer.Element.HandleNavigationCompleted(webView.Url.ToString());
            await renderer.OnJavascriptInjectionRequest(FormsWebView.InjectedFunction);

            if (renderer.Element?.EnableGlobalCallbacks != null && renderer.Element.EnableGlobalCallbacks)
                foreach (var function in FormsWebView.GlobalRegisteredCallbacks)
                    await renderer.OnJavascriptInjectionRequest(FormsWebView.GenerateFunctionScript(function.Key));

            if (renderer.Element?.LocalRegisteredCallbacks != null)
                foreach (var function in renderer.Element.LocalRegisteredCallbacks)
                    await renderer.OnJavascriptInjectionRequest(FormsWebView.GenerateFunctionScript(function.Key));

            if (renderer.Element?.CanGoBack != null)
                renderer.Element.CanGoBack = webView.CanGoBack;

            if (renderer.Element?.CanGoForward != null)
                renderer.Element.CanGoForward = webView.CanGoForward;

            if (renderer.Element?.Navigating != null)
                renderer.Element.Navigating = false;

            renderer.Element?.HandleContentLoaded();
        }
    }
}
