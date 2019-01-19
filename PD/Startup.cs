﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PD.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Hangfire;
using Microsoft.AspNetCore.DataProtection;

namespace PD
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.Configure<CookiePolicyOptions>(options =>
            {
                // This lambda determines whether user consent for non-essential cookies is needed for a given request.
                options.CheckConsentNeeded = context => true;
                options.MinimumSameSitePolicy = SameSiteMode.None;
            });

            string dbConnectionString = Configuration.GetConnectionString("DefaultConnection");
            services.AddDbContext<ApplicationDbContext>(options =>options
                .UseSqlServer(dbConnectionString)
                .ConfigureWarnings(warnings => warnings.Throw(RelationalEventId.QueryClientEvaluationWarning)) //Disables client evaluation
                );

            services.AddIdentity<IdentityUser, IdentityRole>()
                .AddEntityFrameworkStores<ApplicationDbContext>()
                .AddDefaultUI()
                .AddDefaultTokenProviders();

            services.AddAuthentication().AddGoogle(googleOptions =>
            {
                googleOptions.ClientId = Configuration["Authentication:Google:ClientId"];
                googleOptions.ClientSecret = Configuration["Authentication:Google:ClientSecret"];
            });

            services.AddMvc().SetCompatibilityVersion(CompatibilityVersion.Version_2_1);

            services.AddSingleton<IConfiguration>(Configuration);

            //Data protection with SQL key storage
            //Reference: https://docs.microsoft.com/en-us/aspnet/core/security/data-protection/implementation/key-storage-providers?view=aspnetcore-2.2&tabs=visual-studio
            string dataProtectionDbConnectionString = Configuration.GetConnectionString("DataProtectionConnection");
            services.AddDbContext<DataProtectionDbContext>(options => options
                .UseSqlServer(dataProtectionDbConnectionString)
                .ConfigureWarnings(warnings => warnings.Throw(RelationalEventId.QueryClientEvaluationWarning)) //Disables client evaluation
                );
            services.AddDataProtection().PersistKeysToDbContext<DataProtectionDbContext>();

            //HangFire background job processing
            services.AddHangfire(x => x.UseSqlServerStorage(dbConnectionString));
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(
            IApplicationBuilder app,
            IHostingEnvironment env,
            UserManager<IdentityUser> userManager,
            RoleManager<IdentityRole> roleManager)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseDatabaseErrorPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();
            app.UseCookiePolicy();

            app.UseAuthentication();

            app.UseMvc(routes =>
            {
                routes.MapRoute(
                    name: "default",
                    template: "{controller=Home}/{action=Index}/{id?}");
            });


            //Initializing custom roles 
            string[] roleNames = { "Admin", "Manager", "User" };
            foreach (var roleName in roleNames)
            {
                var roleExist = roleManager.RoleExistsAsync(roleName);
                roleExist.Wait();
                if (!roleExist.Result)
                {
                    //create the roles and seed them to the database: Question 1
                    var task = roleManager.CreateAsync(new IdentityRole(roleName));
                    task.Wait();
                }
            }

            //Checking admins
            var checkAdmin = userManager.GetUsersInRoleAsync("Admin");
            checkAdmin.Wait();

            if(checkAdmin.Result.Count() == 0)
            {
                var firstUser = userManager.Users.FirstOrDefault();
                if(firstUser != null)
                {
                    Task task = userManager.AddToRoleAsync(firstUser, "Admin");
                    task.Wait();

                    if (!task.IsCompletedSuccessfully)
                    {
                        throw new Exception("Failed to assign Admin role to default admin user.");
                    }
                }
            }

            //Starting Hangfire background processing server
            app.UseHangfireServer();
            app.UseHangfireDashboard("/hangfire", options: new DashboardOptions { Authorization = new[] { new HangFireAuthorizationFilter() } });

        }
    }
}
