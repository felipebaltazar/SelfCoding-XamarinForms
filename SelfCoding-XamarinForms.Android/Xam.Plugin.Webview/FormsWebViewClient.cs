using Android.Content;
using Android.Graphics;
using Android.Net.Http;
using Android.Runtime;
using Android.Webkit;
using System;
using Xam.Plugin.WebView.Abstractions;
using Xamarin.Essentials;

namespace Xam.Plugin.WebView.Droid
{
    public class FormsWebViewClient : WebViewClient
    {

        readonly WeakReference<FormsWebViewRenderer> Reference;

        public FormsWebViewClient(FormsWebViewRenderer renderer)
        {
            Reference = new WeakReference<FormsWebViewRenderer>(renderer);
        }

        /// Correção para crash <see href="https://appcenter.ms/orgs/Tecfinance/apps/br.com.clear.android/crashes/errors/1407871896u/reports/2517856031954199999-cbad0034-bef0-490d-bf71-90ccabafc8e5/raw"/>
        public FormsWebViewClient(IntPtr javaReference, JniHandleOwnership transfer) : base(javaReference, transfer)
        {
        }

        public override void OnReceivedHttpError(Android.Webkit.WebView view, IWebResourceRequest request, WebResourceResponse errorResponse)
        {
            if (Reference == null || !Reference.TryGetTarget(out FormsWebViewRenderer renderer)) return;
            if (renderer?.Element is null) return;

            renderer.Element.HandleNavigationError(errorResponse.StatusCode);
            renderer.Element.HandleNavigationCompleted(request.Url.ToString());
            renderer.Element.Navigating = false;
        }

        public override void OnReceivedError(Android.Webkit.WebView view, IWebResourceRequest request, WebResourceError error)
        {
            if (Reference == null || !Reference.TryGetTarget(out FormsWebViewRenderer renderer)) return;
            if (renderer.Element == null) return;

            renderer.Element.HandleNavigationError((int) error.ErrorCode);
            renderer.Element.HandleNavigationCompleted(request.Url.ToString());
            renderer.Element.Navigating = false;
        }

        //For Android < 5.0
        [Obsolete]
        public override void OnReceivedError(Android.Webkit.WebView view, [GeneratedEnum] ClientError errorCode, string description, string failingUrl)
        {
            if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.Lollipop) return;

            if (Reference == null || !Reference.TryGetTarget(out FormsWebViewRenderer renderer)) return;
            if (renderer.Element == null) return;

            renderer.Element.HandleNavigationError((int)errorCode);
            renderer.Element.HandleNavigationCompleted(failingUrl.ToString());
            renderer.Element.Navigating = false;
        }

        //For Android < 5.0
        [Obsolete]
        public override WebResourceResponse ShouldInterceptRequest(Android.Webkit.WebView view, string url)
        {
            try
            {
                if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.Lollipop) goto EndShouldInterceptRequest;

                if (Reference == null || !Reference.TryGetTarget(out FormsWebViewRenderer renderer)) goto EndShouldInterceptRequest;
                if (renderer?.Element is null) goto EndShouldInterceptRequest;

                if (string.IsNullOrEmpty(url))
                    goto EndShouldInterceptRequest;

                var response = renderer.Element?.HandleNavigationStartRequest(url);

                if (response != null && (response.Cancel || response.OffloadOntoDevice))
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        if (response?.OffloadOntoDevice ?? false)
                            AttemptToHandleCustomUrlScheme(view, url);

                        view?.StopLoading();
                    });
                }

                EndShouldInterceptRequest:
                    return base.ShouldInterceptRequest(view, url);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                //Retornando null deve carregar normalmente
                //https://developer.android.com/reference/android/webkit/WebViewClient#shouldInterceptRequest(android.webkit.WebView,%20android.webkit.WebResourceRequest)
                return null;
            }            
        }

        public override WebResourceResponse ShouldInterceptRequest(Android.Webkit.WebView view, IWebResourceRequest request)
        {
            try
            {
                if (Reference is null || !Reference.TryGetTarget(out FormsWebViewRenderer renderer)) goto EndShouldInterceptRequest;
                if (renderer?.Element is null) goto EndShouldInterceptRequest;

                var url = request?.Url?.ToString();

                if (string.IsNullOrEmpty(url))
                    goto EndShouldInterceptRequest;

                var response = renderer.Element.HandleNavigationStartRequest(url);

                if (response != null && (response.Cancel || response.OffloadOntoDevice))
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        if (response?.OffloadOntoDevice ?? false)
                            AttemptToHandleCustomUrlScheme(view, url);

                        view?.StopLoading();
                    });
                }

                EndShouldInterceptRequest:
                    return base.ShouldInterceptRequest(view, request);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                //Retornando null deve carregar normalmente
                //https://developer.android.com/reference/android/webkit/WebViewClient#shouldInterceptRequest(android.webkit.WebView,%20android.webkit.WebResourceRequest)
                return null;
            }
            
        }

        void CheckResponseValidity(Android.Webkit.WebView view, string url)
        {
            if (Reference == null || !Reference.TryGetTarget(out FormsWebViewRenderer renderer)) return;
            if (renderer.Element == null) return;

            var response = renderer.Element.HandleNavigationStartRequest(url);

            if (response.Cancel || response.OffloadOntoDevice)
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    try
                    {
                        if (response.OffloadOntoDevice)
                            AttemptToHandleCustomUrlScheme(view, url);

                        view.StopLoading();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex);
                    }
                });
            }
        }

        public override void OnPageStarted(Android.Webkit.WebView view, string url, Bitmap favicon)
        {
            if (Reference == null || !Reference.TryGetTarget(out FormsWebViewRenderer renderer)) return;
            if (renderer.Element == null) return;

            renderer.Element.Navigating = true;
        }

        bool AttemptToHandleCustomUrlScheme(Android.Webkit.WebView view, string url)
        {
            if (view is null)
                return false;

            if (url?.StartsWith("mailto") ?? false)
            {
                Android.Net.MailTo emailData = Android.Net.MailTo.Parse(url);

                Intent email = new Intent(Intent.ActionSendto);

                email.SetData(Android.Net.Uri.Parse("mailto:"));
                email.PutExtra(Intent.ExtraEmail, new String[] { emailData.To });
                email.PutExtra(Intent.ExtraSubject, emailData.Subject);
                email.PutExtra(Intent.ExtraCc, emailData.Cc);
                email.PutExtra(Intent.ExtraText, emailData.Body);

                if (email.ResolveActivity(view.Context.PackageManager) != null)
                    view.Context.StartActivity(email);

                return true;
            }

            if (url?.StartsWith("http") ?? false)
            {
                Intent webPage = new Intent(Intent.ActionView, Android.Net.Uri.Parse(url));
                if (webPage.ResolveActivity(view.Context.PackageManager) != null)
                    view.Context.StartActivity(webPage);

                return true;
            }

            return false;
        }

        public override void OnReceivedSslError(Android.Webkit.WebView view, SslErrorHandler handler, SslError error)
        {
            if (Reference == null || !Reference.TryGetTarget(out FormsWebViewRenderer renderer)) return;
            if (renderer.Element == null) return;

            if (FormsWebViewRenderer.IgnoreSSLGlobally)
            {
                handler.Proceed();
            }

            else
            {
                handler.Cancel();
                renderer.Element.Navigating = false;
            }
        }

        public override void OnPageFinished(Android.Webkit.WebView view, string url)
        {
            try
            {
                if (Reference == null || !Reference.TryGetTarget(out FormsWebViewRenderer renderer)) return;
                if (renderer?.Element is null) return;

                // Add Injection Function
                renderer.JavascriptInjectionRequest(FormsWebView.InjectedFunction);

                // Add Global Callbacks
                if (renderer.Element?.EnableGlobalCallbacks == true)
                    foreach (var callback in FormsWebView.GlobalRegisteredCallbacks)
                        renderer.JavascriptInjectionRequest(FormsWebView.GenerateFunctionScript(callback.Key));

                // Add Local Callbacks
                foreach (var callback in renderer.Element.LocalRegisteredCallbacks)
                    renderer.JavascriptInjectionRequest(FormsWebView.GenerateFunctionScript(callback.Key));

                if (renderer.Element is null)
                    return;

                if (view != null)
                {
                    renderer.Element.CanGoBack = view.CanGoBack();
                    renderer.Element.CanGoForward = view.CanGoForward();
                }

                renderer.Element.Navigating = false;
                renderer.Element?.HandleNavigationCompleted(url);
                renderer.Element?.HandleContentLoaded();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }
    }
}