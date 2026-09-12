using System;
using System.Collections.Generic;
using System.Text;

namespace Infrastructure.Persistence.Data
{
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Design;

    namespace Infrastructure.Persistence.Data
    {
        public class DataContextFactory : IDesignTimeDbContextFactory<DataContext>
        {
            public DataContext CreateDbContext(string[] args)
            {
                var optionsBuilder = new DbContextOptionsBuilder<DataContext>();

                optionsBuilder.UseSqlServer(
                     "Server=(localdb)\\MSSQLLocalDB;Database=EWMS;Trusted_Connection=True;MultipleActiveResultSets=true");

                return new DataContext(optionsBuilder.Options);
            }
        }
    }
}
