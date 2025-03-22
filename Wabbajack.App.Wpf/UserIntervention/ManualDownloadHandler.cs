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
        var uri = default(ManualDownload.BrowserDownloadState);
        try
        {
            var archive = Intervention.Archive;
            var md = Intervention.Archive.State as Manual;

            HeaderText = $"Manual download for {archive.Name} ({md.Url.Host})";

            Instructions = string.IsNullOrWhiteSpace(md.Prompt) ? $"Please download {archive.Name}" : md.Prompt;

            var task = WaitForDownloadUri(token, async () =>
            {
                await RunJavaScript("Array.from(document.getElementsByTagName(\"iframe\")).forEach(f => {if (f.title != \"SP Consent Message\" && !f.src.includes(\"challenges.cloudflare.com\")) f.remove()})");
            });
            await NavigateTo(md.Url);
            await RunJavaScript(@"
            (function() {
                var slowButton = document.getElementById('slowDownloadButton');
                var chek = function(button) {
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
                        slowButton.click();
                        setTimeout(function() {
                            window.close();
                        }, 2000);
                    }
                };
                window.addEventListener('scroll', function() {
                    chek(slowButton);
                });
                chek(slowButton);
            })();");
            uri = await task;
        }
        finally
        {
            Intervention.Finish(uri);
        }
    }
}