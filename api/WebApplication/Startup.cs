using EventSourcingCQRS.Application.Handlers;
using EventSourcingCQRS.Application.PubSub;
using EventSourcingCQRS.Application.Services;
using EventSourcingCQRS.Domain.CartModule;
using EventSourcingCQRS.Domain.EventStore;
using EventSourcingCQRS.Domain.Persistence;
using EventSourcingCQRS.Domain.Persistence.EventStore;
using EventSourcingCQRS.Domain.PubSub;
using EventSourcingCQRS.ReadModel.Customer;
using EventSourcingCQRS.ReadModel.Persistence;
using EventSourcingCQRS.ReadModel.Product;
using EventStore.ClientAPI;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MongoDB.Driver;
using System;
using System.Linq;
using System.Threading.Tasks;

using ReadCart = EventSourcingCQRS.ReadModel.Cart.Cart;
using ReadCartItem = EventSourcingCQRS.ReadModel.Cart.CartItem;

namespace WebApplication
{
    public class Startup
    {
        private const string ReadModelDBName = "ReadModel";

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }


        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllersWithViews();
            
            var connectionString = "ConnectTo=tcp://admin:changeit@localhost:1113; HeartBeatTimeout=500";
            services.AddSingleton(x => EventStoreConnection.Create(connectionString: connectionString));
            services.AddTransient<ITransientDomainEventPublisher, TransientDomainEventPubSub>();
            services.AddTransient<ITransientDomainEventSubscriber, TransientDomainEventPubSub>();
            services.AddTransient<IRepository<Cart, CartId>, EventSourcingRepository<Cart, CartId>>();
            services.AddSingleton<IEventStore, EventStoreEventStore>();
            var mongoSettings = new MongoClientSettings()
            {
                Server = MongoServerAddress.Parse("localhost:1234"),
            };
            services.AddSingleton(x => new MongoClient(mongoSettings));
            services.AddSingleton(x => x.GetService<MongoClient>().GetDatabase(ReadModelDBName));
            services.AddTransient<IReadOnlyRepository<ReadCart>, MongoDBRepository<ReadCart>>();
            services.AddTransient<IRepository<ReadCart>, MongoDBRepository<ReadCart>>();
            services.AddTransient<IReadOnlyRepository<ReadCartItem>, MongoDBRepository<ReadCartItem>>();
            services.AddTransient<IRepository<ReadCartItem>, MongoDBRepository<ReadCartItem>>();
            services.AddTransient<IReadOnlyRepository<Product>, MongoDBRepository<Product>>();
            services.AddTransient<IRepository<Product>, MongoDBRepository<Product>>();
            services.AddTransient<IReadOnlyRepository<Customer>, MongoDBRepository<Customer>>();
            services.AddTransient<IRepository<Customer>, MongoDBRepository<Customer>>();
            services.AddTransient<IDomainEventHandler<CartId, CartCreatedEvent>, CartUpdater>();
            services.AddTransient<IDomainEventHandler<CartId, ProductAddedEvent>, CartUpdater>();
            services.AddTransient<IDomainEventHandler<CartId, ProductQuantityChangedEvent>, CartUpdater>();
            services.AddTransient<ICartWriter, CartWriter>();
            services.AddTransient<ICartReader, CartReader>();
        }

        public void Configure(IApplicationBuilder app, 
            IWebHostEnvironment env,
            IEventStoreConnection conn, 
            IRepository<Product> productRepository,
            IRepository<Customer> customerRepository)
        {
            if (env.IsDevelopment())
                app.UseDeveloperExceptionPage();
            else
                app.UseExceptionHandler("/Home/Error");

            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseRouting();
            app.UseEndpoints(endpoints => {
                endpoints.MapControllerRoute(
                    name: "default",
                    pattern: "{controller=Carts}/{action=IndexAsync}/{id?}");
            });

            conn.ConnectAsync().Wait();

            if (!productRepository.FindAllAsync(x => true).Result.Any() && !customerRepository.FindAllAsync(x => true).Result.Any())
                SeedReadModel(productRepository, customerRepository);
        }
        

        private void SeedReadModel(IRepository<Product> productRepository, IRepository<Customer> customerRepository)
        {
            var insertingProducts = new[] {
                new Product
                {
                    Id = $"Product-{Guid.NewGuid()}",
                    Name = "Laptop"
                },
                new Product
                {
                    Id = $"Product-{Guid.NewGuid()}",
                    Name = "Smartphone"
                },
                new Product
                {
                    Id = $"Product-{Guid.NewGuid()}",
                    Name = "Gaming PC"
                },
                new Product
                {
                    Id = $"Product-{Guid.NewGuid()}",
                    Name = "Microwave oven"
                },
            }
            .Select(x => productRepository.InsertAsync(x));

            var insertingCustomers = new Customer[] {
                new Customer
                {
                    Id = $"Customer-{Guid.NewGuid()}",
                    Name = "Andrea"
                },
                new Customer
                {
                    Id = $"Customer-{Guid.NewGuid()}",
                    Name = "Martina"
                },
            }
            .Select(x => customerRepository.InsertAsync(x));

            Task.WaitAll(insertingProducts.Union(insertingCustomers).ToArray());
        }
    }
}
