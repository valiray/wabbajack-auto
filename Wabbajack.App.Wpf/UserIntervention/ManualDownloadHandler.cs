using Microsoft.Web.WebView2.Core;
using System;
using System.Threading;
using System.Threading.Tasks;
using Wabbajack.DTOs.DownloadStates;
using Wabbajack.DTOs.Interventions;

namespace Wabbajack;

public class ManualDownloadHandler : BrowserWindowViewModel
{
    public ManualDownload Intervention { get; set; }

    public ManualDownloadHandler(IServiceProvider serviceProvider) : base(serviceProvider) { }

    protected override async Task Run(CancellationToken token)
    {
        var dowloadState = default(ManualDownload.BrowserDownloadState);
        try
        {
            var archive = Intervention.Archive;
            var md = Intervention.Archive.State as Manual;

            HeaderText = $"Manual download for {archive.Name} ({md.Url.Host})";

            Instructions = string.IsNullOrWhiteSpace(md.Prompt) ? $"Please download {archive.Name}" : md.Prompt;

            dowloadState = await NavigateAndLoadDownloadState(md.Url, token);
        }
        finally
        {
            Intervention.Finish(dowloadState);
        }
    }

    private async Task<ManualDownload.BrowserDownloadState> NavigateAndLoadDownloadState(Uri downloadPageUrl, CancellationToken token)
    {
        var source = new TaskCompletionSource<Uri>();
        var referer = Browser.Source;
        await WaitForReady(token);

        EventHandler<CoreWebView2DownloadStartingEventArgs> handler = null!;

        handler = (_, args) =>
        {
            try
            {
                source.TrySetResult(new Uri(args.DownloadOperation.Uri));
            }
            catch (Exception)
            {
                source.TrySetCanceled(token);
            }

            args.Cancel = true;
            args.Handled = true;
        };

        Browser.CoreWebView2.DownloadStarting += handler;

        // 1. Navigate to the page
        await NavigateTo(downloadPageUrl);

        // 2. Inject the Nexus Mods auto-click script if we are on Nexus Mods
        if (downloadPageUrl.Host.EndsWith("nexusmods.com", StringComparison.OrdinalIgnoreCase))
        {
            await RunJavaScript(@"
            (function() {
                // Helper to penetrate the Shadow DOM and find the slow download button
                var getSlowButton = function() {
                    // Try the legacy method first just in case
                    var btn = document.getElementById('slowDownloadButton');
                    if (btn) return btn;
                    
                    // Target the new Web Component Nexus Mods uses
                    var modComponent = document.querySelector('mod-file-download');
                    if (modComponent && modComponent.shadowRoot) {
                        // Search inside the shadow root by ID
                        btn = modComponent.shadowRoot.querySelector('#slowDownloadButton');
                        if (btn) return btn;
                        
                        // Ultimate fallback: search for the text inside all buttons
                        var buttons = modComponent.shadowRoot.querySelectorAll('button');
                        for (var i = 0; i < buttons.length; i++) {
                            if (buttons[i].textContent.trim().toLowerCase().includes('slow download')) {
                                return buttons[i];
                            }
                        }
                    }
                    return null;
                };

                var clicked = false;

                var chek = function() {
                    if (clicked) return;
                    
                    var button = getSlowButton();
                    if (!button) return; // Wait until it's rendered

                    var PosT = {
                            top: window.pageYOffset + button.getBoundingClientRect().top,
                            left: window.pageXOffset + button.getBoundingClientRect().left,
                            right: window.pageXOffset + button.getBoundingClientRect().right,
                            bottom: window.pageYOffset + button.getBoundingClientRect().bottom
                        },
                        PosW = {
                            top: window.pageYOffset,
                            left: window.pageXOffset,
                            right: window.pageXOffset + document.documentElement.clientWidth,
                            bottom: window.pageYOffset + document.documentElement.clientHeight
                        };
                        
                    if (PosT.bottom > PosW.top &&
                        PosT.top < PosW.bottom &&
                        PosT.right > PosW.left &&
                        PosT.left < PosW.right) {
                        
                        clicked = true; // Lock to prevent multi-clicking on rapid scrolls
                        button.click();
                        setTimeout(function() {
                            window.close();
                        }, 2000);
                    }
                };

                window.addEventListener('scroll', function() {
                    chek();
                });
                
                // Initial run. Adding slight timeouts because Web Components 
                // sometimes take a few milliseconds to render their Shadow DOM content.
                chek();
                setTimeout(chek, 500);
                setTimeout(chek, 1500);
            })();");
        }

        try
        {
            // 3. Wait for the user (or script) to trigger the download, and catch it via iframe check
            var uri = await base.WaitWhileRemovingIframes(source.Task, token);

            var cookies = await GetCookies(uri.Host, token);

            return new ManualDownload.BrowserDownloadState(
                uri,
                cookies,
                new[]
                {
                ("Referer", referer?.ToString() ?? uri.ToString())
                },
                Browser.CoreWebView2.Settings.UserAgent);
        }
        finally
        {
            Browser.CoreWebView2.DownloadStarting -= handler;
        }

    }
}