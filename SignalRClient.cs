using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR;

namespace SignalR.Persistent.Client
{
    public class SignalRClient
    {
        private HubConnection _connection;
        private static System.Timers.Timer _pingTimer;
        private static readonly Random StaticRandom = new Random();
        private readonly string _groupName = "{groupName}";
        private readonly string _hubUrl = $"{ApplicationURL}/SignalRHub";
        
        public async Task StartConnection()
        {
            try
            {
                _connection = new HubConnectionBuilder()
                    .WithUrl(_hubUrl, options =>
                    {
                        options.Transports = HttpTransportType.WebSockets;
                    })
                    .WithAutomaticReconnect(new AlwaysRetryPolicy())
                    .Build();

                _connection.ServerTimeout = TimeSpan.FromMinutes(3);
                _connection.KeepAliveInterval = TimeSpan.FromSeconds(30);

                _connection.Reconnected += async (connectionId) =>
                {
                    try
                    {

                        // On Reconnect We need to rejoin the group as connection is new for signal-r server
                        await WaitForSeconds(3);
                        await _connection.InvokeAsync(_groupName);
                    }
                    catch (Exception ex)
                    {
                        await WaitForSeconds(5);
                        await StartAsyncWithRetry(_connection);
                    }
                };

                _connection.Closed += async (error) =>
                {
                    try
                    {
                        await WaitForSeconds(5);
                        await StartAsyncWithRetry(_connection);
                    }
                    catch (Exception ex)
                    {
                        await WaitForSeconds(5);
                        await StartAsyncWithRetry(_connection);
                    }
                };

                await StartAsyncWithRetry(_connection);
            }
            catch (HubException hubEx)
            {
				await WaitForSeconds(5);
                await StartConnection();
            }
            catch (Exception ex)
            {
				await WaitForSeconds(5);
                await StartConnection();
            }
        }

        private async Task WaitForSeconds(int seconds)
        {
            await Task.Delay(seconds * 1000);
        }

        private async Task StartAsyncWithRetry(HubConnection connection, CancellationToken cancellationToken = default)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await connection.StartAsync(cancellationToken);
                    await connection.InvokeAsync(_groupName);
                    StartPeriodicPing(connection);
                    return;
                }
                catch (HubException ex) 
                {
                    await Task.Delay(RandomDelayMilliseconds(), 
					cancellationToken);
                }
                catch (Exception ex)
                {
                    await Task.Delay(RandomDelayMilliseconds(), 
					cancellationToken);
                }
            }
        }

        private void StartPeriodicPing(HubConnection connection)
        {
            try
            {
                _pingTimer?.Dispose();
                _pingTimer = new System.Timers.Timer(60000); // Every 1 minute

                _pingTimer.Elapsed += async (sender, e) =>
                {
                    if (connection.State == HubConnectionState.Connected)
                    {
                        await connection.InvokeAsync("Ping");
                    }
                };

                _pingTimer.Start();
            }
            catch (Exception ex)
            {
                _pingTimer?.Dispose();
            }
        }

        private sealed class AlwaysRetryPolicy : IRetryPolicy
        {
            public TimeSpan? NextRetryDelay(RetryContext retryContext)
            {
                return TimeSpan.FromMilliseconds(RandomDelayMilliseconds());
            }
        }

        private static int RandomDelayMilliseconds()
        {
            // Delay will be between 5 to 10 secs
            return StaticRandom.Next(5000, 10000);
        }
    }
}
