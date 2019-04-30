﻿using Microsoft.Extensions.Hosting;
using Raven.Client.Documents;
using Raven.Client.Documents.Commands.Batches;
using Raven.Client.Documents.Operations;
using Raven.Client.Documents.Subscriptions;
using RavenDbSample.Auditable;
using RavenDbSample.Models;
using Sparrow.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace RavenDbSample
{
	public sealed class RavenDbAuditProcessor : IHostedService, IDisposable
	{
		private readonly IDocumentStore _store;
		private List<Task> _subscriptionWorkerTasks = new List<Task>();
		private CancellationTokenSource _tokenSource;
		private readonly object _gate = new object();
		private readonly HashSet<string> _names = new HashSet<string>();
		private const string _prefixName= "AuditProcessor";
		
		public RavenDbAuditProcessor(IDocumentStore store, IAuditableTypeProvider provider)
		{
			_store = store;

			foreach (var t in provider.TypeList)
				_names.Add(store.Conventions.GetCollectionName(t));

		}

		public async Task StartAsync(CancellationToken ctk = default)
		{
			foreach (var name in _names)
			{
				try
				{
					var localName = await _store.Subscriptions.CreateAsync(new SubscriptionCreationOptions()
					{
						Name = _prefixName + name,
						Query = $@"From {name}(Revisions = true)"
					});
				}
				catch (Exception e) when (e.Message.Contains("is already in use in a subscription with different Id"))
				{
				}
			}

			lock (_gate)
			{
				if (_subscriptionWorkerTasks.Count > 0)
					throw new InvalidOperationException("Already started");

				_tokenSource = new CancellationTokenSource();

				foreach (var name in _names)
				{
					_subscriptionWorkerTasks.Add(Task.Run(() => _run(name, _tokenSource.Token), ctk));
				}
			}
		}

		private async Task _run(string name, CancellationToken ctk = default)
		{
			while (!ctk.IsCancellationRequested)
			{
				try
				{
					using (var worker = _store.Subscriptions.GetSubscriptionWorker<Revision<dynamic>>(
					new SubscriptionWorkerOptions(_prefixName + name)
					{
						Strategy = SubscriptionOpeningStrategy.WaitForFree,
						MaxDocsPerBatch = 10,
					}))
					{

						await worker.Run(_processAuditChange, ctk);
					}
				}
				catch (TaskCanceledException) { throw; }
				catch (Exception)
				{
					// retry
				}
			}

		}

		private async Task _processAuditChange(SubscriptionBatch<Revision<dynamic>> batch)
		{
			using (var session = _store.OpenAsyncSession())
			{
				foreach (var e in batch.Items)
				{
					if (e.Result.Current?.AuditId != null)
					{
						session.Advanced.Defer(new PatchCommandData(
							id: (string)e.Result.Current.AuditId,
							changeVector: null,
							patch: new PatchRequest
							{
								Script = @"this.EntityInfo
											.forEach(eInfo => { 
												if (eInfo.EntityId == args.Id) 
													eInfo.CurrChangeVector = args.Cv; 
											});
										 ",
								Values =
								{
									{
										"Cv", e.ChangeVector
									},
									{
										"Id", e.Id
									}
								}
								//Script = "this.EntityInfo[args.Id].CurrChangeVector = args.cv;",
								//Values =
								//{
								//	{
								//		"cv", e.ChangeVector
								//	},
								//	{
								//		"Id", e.Id
								//	}
								//}
							},
							patchIfMissing: null));
					}
				}

				await session.SaveChangesAsync();
			}
		}

		public async Task StopAsync(CancellationToken ctk = default)
		{
			List<Task> runtask = new List<Task>();
			lock (_gate)
			{
				_tokenSource.Cancel();
				_tokenSource = null;
				runtask.AddRange(_subscriptionWorkerTasks);
				_subscriptionWorkerTasks.Clear();
			}

			try
			{
				await Task.WhenAll(runtask);
			}
			catch (TaskCanceledException) { }
		}

		public void Dispose()
		{
			_tokenSource?.Cancel();
			_tokenSource?.Dispose();
		}
	}
}
