﻿using IGDiscord.Models.MessageInfo;
using IGDiscord.Models;
using IGDiscord.Utils;
using IGDiscord.Services.Interfaces;
using CounterStrikeSharp.API.Core;
using Microsoft.Extensions.Logging;
using CounterStrikeSharp.API;
using IGDiscord.Models.Discord;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using System.Threading;
using static CounterStrikeSharp.API.Core.Listeners;
using System.Net;

namespace IGDiscord;

public partial class IGDiscord : BasePlugin, IPluginConfig<Config>
{
    public override string ModuleName => "IGDiscord";
    public override string ModuleVersion => "1.0.0";
    public override string ModuleAuthor => "raz";
    public override string ModuleDescription => "A Discord webhook plugin for Imperfect Gamers";

    public Config Config { get; set; }
    public string ConfigPath;

    public StatusData _statusData = new();
    private WebhookMessage _webhookMessage;

    private readonly IConfigService _configService;
    private readonly IDiscordService _discordService;
    private readonly ILogger<IGDiscord> _logger;

    public IGDiscord(
        IConfigService configService,
        IDiscordService discordService,
        ILogger<IGDiscord> logger)
    {
        _configService = configService;
        _discordService = discordService;
        _logger = logger;
    }

    public void OnHostNameChanged(string hostName)
    {
        _statusData.ServerOnline = true;
        _statusData.ServerName = hostName;
        _statusData.MapName = Server.MapName;

        Task.Run(async () =>
        {
            await GetIpAddress();
        });

        if (string.IsNullOrEmpty(Config.StatusInfo.MessageId))
        {
            CreateDiscordStatusMessage();
        }

        UpdateDiscordStatusMessage();
    }

    public void OnMapStart(string mapName)
    {
        _statusData.ServerOnline = true;
        _statusData.MapName = mapName;

        Task.Run(async () =>
        {
            await GetIpAddress();
        });

        UpdateDiscordStatusMessage();
    }

    public override void Load(bool hotReload)
    {
        if (Config != null)
        {
            _statusData.Timestamp = DateTime.Now;

            _webhookMessage = _discordService.CreateWebhookMessage(Config.StatusInfo, _statusData);

            RegisterListener<Listeners.OnHostNameChanged>(OnHostNameChanged);
            RegisterListener<Listeners.OnMapStart>(OnMapStart);
        }
        else
        {
            _logger.LogInformation("The config file did not load correctly. Please check that there is a {ModuleName}.json file in the CounterStrikeSharp config directory.", ModuleName);
        };
    }
    
    public override void Unload(bool hotReload)
    {
        base.Unload(hotReload);
    }

    private void CreateDiscordStatusMessage()
    {
        // Send initial message
        Task.Run(async () =>
        {
            var messageId = await _discordService.CreateStatusMessageAsync(Config.StatusInfo, _webhookMessage);

            if (!string.IsNullOrEmpty(messageId))
            {
                Config.StatusInfo.MessageId = messageId;

                _configService.UpdateConfig(Config, ConfigPath);
            }
            else
            {
                Util.PrintError("Something went wrong getting a response when sending message.");
            }
        });
    }

    private void UpdateDiscordStatusMessage()
    {
        // Update the message
        Task.Run(async () =>
        {
            _statusData.Timestamp = DateTime.Now;

            WebhookMessage updatedWebhookMessage = _discordService.UpdateWebhookMessage(_webhookMessage, _statusData);

            await _discordService.UpdateStatusMessageAsync(Config.StatusInfo, updatedWebhookMessage);
        });
    }

    public void OnConfigParsed(Config config)
    {
        ConfigPath = _configService.GetConfigPath(ModuleDirectory, ModuleName);

        if (File.Exists(ConfigPath) is false)
        {
            Util.PrintLog($"Creating {ModuleName}.json for the first time. ");

            config = new Config();
        }

        Config = config;
    }

    private async Task GetIpAddress()
    {
        using var httpClient = new HttpClient();

        var dynDnsResponse = await httpClient.GetStringAsync("http://checkip.dyndns.org");

        var dynDnsResponseTrimmed = dynDnsResponse.Split(':')[1].Split('<')[0].Trim();

        if (!IPAddress.TryParse(dynDnsResponseTrimmed, out var ipAddress))
        {
            _statusData.IpAddress = "IP address not found";
        }

        _statusData.IpAddress = ipAddress.ToString() + ":27015";

        Util.PrintLog(_statusData.IpAddress);
    }
}