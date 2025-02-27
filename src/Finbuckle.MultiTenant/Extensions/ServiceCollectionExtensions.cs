// Copyright Finbuckle LLC, Andrew White, and Contributors.
// Refer to the solution LICENSE file for more inforation.

using System;
using System.Linq;
using Finbuckle.MultiTenant;
using Finbuckle.MultiTenant.Core;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class FinbuckleServiceCollectionExtensions
    {
        /// <summary>
        /// Configure Finbuckle.MultiTenant services for the application.
        /// </summary>
        /// <param name="services">The IServiceCollection<c/> instance the extension method applies to.</param>
        /// <param name="config">An action to configure the MultiTenantOptions instance.</param>
        /// <returns>An new instance of MultiTenantBuilder.</returns>
        public static FinbuckleMultiTenantBuilder<T> AddMultiTenant<T>(this IServiceCollection services, Action<MultiTenantOptions> config)
            where T : class, ITenantInfo, new()
        {
            services.AddScoped<ITenantResolver<T>, TenantResolver<T>>();
            services.AddScoped<ITenantResolver>(sp => (ITenantResolver)sp.GetRequiredService<ITenantResolver<T>>());

            services.AddScoped<IMultiTenantContext<T>>(sp => sp.GetRequiredService<IMultiTenantContextAccessor<T>>().MultiTenantContext);
            
            services.AddScoped<T>(sp => sp.GetRequiredService<IMultiTenantContextAccessor<T>>().MultiTenantContext?.TenantInfo);
            services.AddScoped<ITenantInfo>(sp => sp.GetService<T>());
            
            services.AddSingleton<IMultiTenantContextAccessor<T>, MultiTenantContextAccessor<T>>();
            services.AddSingleton<IMultiTenantContextAccessor>(sp => (IMultiTenantContextAccessor)sp.GetRequiredService<IMultiTenantContextAccessor<T>>());
            
            services.Configure<MultiTenantOptions>(config);
            
            return new FinbuckleMultiTenantBuilder<T>(services);
        }

        /// <summary>
        /// Configure Finbuckle.MultiTenant services for the application.
        /// </summary>
        /// <param name="services">The IServiceCollection<c/> instance the extension method applies to.</param>
        /// <returns>An new instance of MultiTenantBuilder.</returns>
        public static FinbuckleMultiTenantBuilder<T> AddMultiTenant<T>(this IServiceCollection services)
            where T : class, ITenantInfo, new()
        {
            return services.AddMultiTenant<T>(_ => { });
        }

        public static bool DecorateService<TService, TImpl>(this IServiceCollection services, params object[] parameters)
        {
            var existingService = services.SingleOrDefault(s => s.ServiceType == typeof(TService));
            if (existingService == null)
                return false;

            var newService = new ServiceDescriptor(existingService.ServiceType,
                                           sp =>
                                           {
                                               TService inner = (TService)ActivatorUtilities.CreateInstance(sp, existingService.ImplementationType);

                                               var parameters2 = new object[parameters.Length + 1];
                                               Array.Copy(parameters, 0, parameters2, 1, parameters.Length);
                                               parameters2[0] = inner;

                                               return ActivatorUtilities.CreateInstance<TImpl>(sp, parameters2);
                                           },
                                           existingService.Lifetime);

            if (existingService.ImplementationInstance != null)
            {
                newService = new ServiceDescriptor(existingService.ServiceType,
                                           sp =>
                                           {
                                               TService inner = (TService)existingService.ImplementationInstance;
                                               return ActivatorUtilities.CreateInstance<TImpl>(sp, inner, parameters);
                                           },
                                           existingService.Lifetime);
            }
            else if (existingService.ImplementationFactory != null)
            {
                newService = new ServiceDescriptor(existingService.ServiceType,
                                           sp =>
                                           {
                                               TService inner = (TService)existingService.ImplementationFactory(sp);
                                               return ActivatorUtilities.CreateInstance<TImpl>(sp, inner, parameters);
                                           },
                                           existingService.Lifetime);
            }

            services.Remove(existingService);
            services.Add(newService);

            return true;
        }
    }
}