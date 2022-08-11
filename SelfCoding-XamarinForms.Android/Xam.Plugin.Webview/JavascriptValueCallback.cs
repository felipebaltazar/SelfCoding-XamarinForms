using System;
using System.Threading.Tasks;
using Android.Webkit;

namespace Xam.Plugin.WebView.Droid
{
    public class JavascriptValueCallback : Java.Lang.Object, IValueCallback
    {

        public Java.Lang.Object Value { get; private set; }

        public TaskCompletionSource<string> TaskCallback { get; set; }

        readonly WeakReference<FormsWebViewRenderer> Reference;

        public JavascriptValueCallback(FormsWebViewRenderer renderer)
        {
            Reference = new WeakReference<FormsWebViewRenderer>(renderer);
        }

        [Obsolete("Solução para crash")]
        public JavascriptValueCallback(System.IntPtr intPtr, Android.Runtime.JniHandleOwnership ownership) : base(intPtr, ownership)
        {
        }

        public void OnReceiveValue(Java.Lang.Object value)
        {
            Value = value;
            TaskCallback?.TrySetResult(value?.ToString());
        }

        public void Reset()
        {
            Value = null;
        }
    }
}