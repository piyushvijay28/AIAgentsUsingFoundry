using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

public class ShopAxisDbContextFactory : IDesignTimeDbContextFactory<ShopAxisDbContext>
{
    public ShopAxisDbContext CreateDbContext(string[] args)
    {
        // Read from environment variable — same as your app uses
        var connectionString =
            Environment.GetEnvironmentVariable("SQL_CONNECTION_STRING")
            ?? throw new InvalidOperationException(
                "Missing SQL_CONNECTION_STRING environment variable.\n" +
                "Run: $env:SQL_CONNECTION_STRING = 'your-connection-string'");

        var optionsBuilder = new DbContextOptionsBuilder<ShopAxisDbContext>();
        optionsBuilder.UseSqlServer(connectionString);

        return new ShopAxisDbContext(optionsBuilder.Options);
    }
}