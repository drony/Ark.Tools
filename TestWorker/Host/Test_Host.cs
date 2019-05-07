﻿using NodaTime;
using System;
using System.Threading;
using System.Threading.Tasks;
using TestWorker.Dto;
using Ark.Tools.ResourceWatcher.WorkerHost;
using TestWorker.DataProvider;
using TestWorker.Configs;
using TestWorker.Writer;
using Ark.Tools.ResourceWatcher;
using Ark.Tools.ResourceWatcher.ApplicationInsights;
using Ark.Tools.ResourceWatcher.WorkerHost.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace TestWorker.Host
{
    public static class Test_Host
    {
        public static IHostBuilder ConfigureFromAppSettings(bool ignoreStateServiceInDev = true, bool useSingleThread = false, Action<ITest_Host_Config> configurationOverrider = null)
        {
            try
            {
                var hostBuilder = new HostBuilder()
                    .AddWorkerHostInfrastracture()
                    .AddApplicationInsightsForWorkerHost()
                    .AddWorkerHost<Host>(s =>
                    {
                        //var config = s.GetService<IConfiguration>();
                        var baseCfg1 = new Test_Host_Config()
                        {
                            StateDbConnectionString = "" //config.GetConnectionString("boh")
                        };

                        configurationOverrider?.Invoke(baseCfg1);

                        return new Host(baseCfg1)
                            .WithTestWriter();
                    })
                    .UseConsoleLifetime();

                return hostBuilder;
            }
            catch (Exception e)
            {
                throw e;
            }
        }

        #region Host Class
        public class Host : WorkerHost<Test_File, Test_FileMetadataDto, Test_ProviderFilter>
        {
            public Host(ITest_Host_Config config) : base(config)
            {
                this.UseDataProvider<TestProvider>(deps =>
                {

                });

                this.UseStateProvider<InMemStateProvider>();
            }

            public Task RunOnceAsync(LocalDate date, CancellationToken ctk = default)
            {
                return base.RunOnceAsync(f => f.Date = LocalDateTime.FromDateTime(DateTime.Today), ctk);
            }

            public Host WithTestWriter()
            {
                this.AppendFileProcessor<TestWriter>(deps =>
                {
                    //deps.Container.RegisterInstance(cfg);
                });

                return this;
            }

        }
        #endregion
    }


}