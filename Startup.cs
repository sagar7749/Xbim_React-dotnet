using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xbim.Ifc;

public class Startup
{
    public Startup(IConfiguration configuration)
    {
        Configuration = configuration;

        // Configure Xbim services here
        IfcStore.ModelProviderFactory.UseHeuristicModelProvider();
    }

    public IConfiguration Configuration { get; }

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddCors(options =>
        {
            options.AddPolicy("AllowSpecificOrigin",
                builder => builder.WithOrigins("http://127.0.0.1:5173") // Add your frontend URL here
                    .AllowAnyMethod()
                    .AllowAnyHeader());
        });

        services.AddControllers();
        services.AddLogging(); // Add logging services
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }
        else
        {
            app.UseExceptionHandler("/Home/Error");
            app.UseHsts();
        }

        app.UseHttpsRedirection();
        app.UseStaticFiles();
        // Use CORS with the policy
        app.UseCors("AllowSpecificOrigin");
        app.UseRouting();
        app.UseAuthorization();

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllers();
        });
    }
}
