﻿using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Raven.Client.Documents;
using Raven.Embedded;
using Microsoft.Extensions.DependencyInjection;
using Raven.Client.Documents.Session;
using RavenDbSample.Models;
using Raven.Client.Documents.Operations.Revisions;
using RavenDbSample.Auditable.Decorator;
using System.Collections.Generic;
using System;
using System.Threading.Tasks;

namespace RavenDbSample
{
	public class Program
	{
		public static void Main(string[] args)
		{
			EmbeddedServer.Instance.StartServer(new ServerOptions
			{
				ServerUrl = "http://127.0.0.1:8080"
			});

			var store = EmbeddedServer.Instance.GetDocumentStore(new DatabaseOptions("RavenDb7")
			{
			});

			store.Maintenance.Send(new ConfigureRevisionsOperation(new RevisionsConfiguration
			{
				Default = new RevisionsCollectionConfiguration
				{
					Disabled = false,
					PurgeOnDelete = false,
					MinimumRevisionsToKeep = null,
					MinimumRevisionAgeToKeep = null,
				}
			}));

			CreateWebHostBuilder(args)
				.ConfigureServices((c, s) => {
					s.AddSingleton<IDocumentStore>(store);
				//	//s.AddScoped(ss => ss.GetService<IDocumentStore>().OpenAsyncSession());					
				//	s.AddHostedService<RavenDbAuditProcessor>();
				//	s.AddSingleton<IAuditableTypeProvider>(ss => new AuditableTypeProvider(types));
				//	s.AddScoped<IAsyncDocumentSession>(ss => new AsyncDocumentSessionDecorator(ss.GetService<IDocumentStore>().OpenAsyncSession()));
				})				
				.Build()
				.Run();
		}

		public static IWebHostBuilder CreateWebHostBuilder(string[] args) =>
			WebHost.CreateDefaultBuilder(args)
				.UseStartup<Startup>();
	}


}
