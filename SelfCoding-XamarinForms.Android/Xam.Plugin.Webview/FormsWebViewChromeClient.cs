using Android.Webkit;
using System;

namespace Xam.Plugin.WebView.Droid
{
    public class FormsWebViewChromeClient : WebChromeClient
    {
        readonly WeakReference<FormsWebViewRenderer> Reference;

        public FormsWebViewChromeClient(FormsWebViewRenderer renderer)
        {
            Reference = new WeakReference<FormsWebViewRenderer>(renderer);
        }
    }
}