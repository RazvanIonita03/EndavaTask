using CarInsurance.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace CarInsurance.Api.Tests.Helpers;

public static class TestDbContextFactory
{
	public static AppDbContext CreateInMemoryDbContext(string databaseName)
	{
		var options = new DbContextOptionsBuilder<AppDbContext>()
			.UseInMemoryDatabase(databaseName)
			.EnableSensitiveDataLogging()
			.Options;

		var context = new AppDbContext(options);
		context.Database.EnsureCreated();
		return context;
	}
}
