using Cerebro.Server.Data;
var store = new SqliteDashboardCredentialsStore("Data Source=/tmp/cerebro-httptest.db");
await store.SetCredentialsAsync("surveillant", "testpw123", CancellationToken.None);
Console.WriteLine("done");
