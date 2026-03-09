using Domino.Server.Networking;
using System;
using System.Threading;

// CancellationTokenSource lets us stop the server cleanly with Ctrl+C
var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true; // don't kill the process immediately
    cts.Cancel();    // signal all async operations to stop
    Console.WriteLine("[Server] Shutting down...");
};

var server = new TcpServer(port: 5000);
await server.StartAsync(cts.Token);
