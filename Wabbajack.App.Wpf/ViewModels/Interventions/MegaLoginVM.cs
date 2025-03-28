﻿using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Windows;
using System.Windows.Input;
using CG.Web.MegaApiClient;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Wabbajack.DTOs.Logins;
using Wabbajack.Messages;
using Wabbajack.Networking.WabbajackClientApi;
using Wabbajack.RateLimiter;
using Wabbajack.Services.OSIntegrated.TokenProviders;
using static CG.Web.MegaApiClient.MegaApiClient;

namespace Wabbajack;

public class MegaLoginVM : ViewModel
{

    private readonly ILogger<MegaLoginVM> _logger;
    private readonly MegaTokenProvider _tokenProvider;
    private readonly MegaApiClient _apiClient;

    public ICommand CloseCommand { get; }
    public ICommand LoginCommand { get; }
    public ICommand LoginAnonymouslyCommand { get; }

    [Reactive] public double UploadProgress { get; set; }
    [Reactive] public string FileUrl { get; set; }
    public FilePickerVM Picker { get;}
    
    [Reactive] public string Email { get; set; }
    [Reactive] public string Password { get; set; }
    [Reactive] public AuthInfos Login { get; private set; }

    [Reactive] public bool LoggingIn { get; set; }
    [Reactive] public bool LoginSuccessful { get; set; }
    [Reactive] public bool TriedLoggingIn { get; set; }

    public MegaLoginVM(ILogger<MegaLoginVM> logger, MegaTokenProvider tokenProvider, Client wjClient, SettingsVM vm, MegaApiClient apiClient)
    {
        _logger = logger;
        _tokenProvider = tokenProvider;
        _apiClient = apiClient;

        CloseCommand = ReactiveCommand.Create(async () =>
        {
            ShowFloatingWindow.Send(FloatingScreenType.None);
        });

        LoginCommand = ReactiveCommand.Create(async (bool anonymous) =>
        {
            TriedLoggingIn = true;
            LoggingIn = true;
            // Since the login task can gets stuck on a failed login, cancel the login task if it hasn't returned after 30s
            using var tokenSource = new CancellationTokenSource();
            tokenSource.CancelAfter(TimeSpan.FromSeconds(30));
            try
            {
                if (anonymous)
                {
                    await DoLoginAnonymously(tokenSource.Token);
                    LoggedIntoMega.Send(null);
                }
                else
                {
                    var (auth, loginToken) = await DoLogin(tokenSource.Token);
                    LoggedIntoMega.Send(auth);
                }
                LoginSuccessful = true;

                // To show the user they're logged in before closing
                await Task.Delay(500);
                CloseCommand.Execute(null);
            }
            catch (Exception ex)
            {
                if (ex is OperationCanceledException)
                    _logger.LogError("Request timed out, MEGA login cancelled!");

                _logger.LogError("Failed to log into MEGA: {ex}", ex.ToString());
                LoginSuccessful = false;
            }
            finally
            {
                LoggingIn = false;
            }
        }, this.WhenAnyValue(vm => vm.LoggingIn, loggingIn => !loggingIn));

        LoginAnonymouslyCommand = ReactiveCommand.Create(() => LoginCommand.Execute(true), this.WhenAnyValue(vm => vm.LoggingIn, loggingIn => !loggingIn));

        this.WhenActivated(disposables =>
        {
            TriedLoggingIn = false;

            Disposable.Empty.DisposeWith(disposables);
        });
    }

    private async Task<(AuthInfos, LogonSessionToken)> DoLogin(CancellationToken token)
    {
        var auth = await _apiClient.GenerateAuthInfosAsync(Email, Password).WaitAsync(token);
        return (auth, await _apiClient.LoginAsync(auth).WaitAsync(token));
    }
    private async Task DoLoginAnonymously(CancellationToken token)
    {
        await _apiClient.LoginAnonymousAsync().WaitAsync(token);
    }
}
