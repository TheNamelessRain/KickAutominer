using KickAutominer.ViewModels;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using System.Windows;

namespace KickAutominer.Attached
{
    public static class WebView2Initializer
    {
        public static readonly DependencyProperty InitializeProperty =
            DependencyProperty.RegisterAttached(
                "Initialize",
                typeof(bool),
                typeof(WebView2Initializer),
                new PropertyMetadata(false, OnInitializeChanged));

        public static void SetInitialize(WebView2 element, bool value)
            => element.SetValue(InitializeProperty, value);

        public static bool GetInitialize(WebView2 element)
            => (bool)element.GetValue(InitializeProperty);

        private static async void OnInitializeChanged(
            DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is WebView2 webView && (bool)e.NewValue)
            {
                webView.CoreWebView2InitializationCompleted += async (s, ev) =>
                {
                    if (webView.DataContext is MainViewModel vm)
                    {
                        vm.WebViewCore = webView.CoreWebView2;
                        await vm.InitializeWebViewAsync();
                    }
                };

                if (webView.CoreWebView2 == null)
                    await webView.EnsureCoreWebView2Async();
            }
        }
    }
}
