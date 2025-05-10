using System.Linq.Expressions;
using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using MudBlazor;
using Humanizer;
using Microsoft.AspNetCore.Mvc;
using STZ.Shared.Dtos;

namespace STZ.Shared.Bases;

public class ServiceBase<T> where T : class
{
    protected readonly HttpClient HttpClient;
    protected readonly string Endpoint;
    
    public ServiceBase(HttpClient httpClient, IConfiguration configuration)
    {
        HttpClient = httpClient;
        var baseUrl = configuration.GetSection("ApiSettings:BaseUrl").Value;
        var entityName = typeof(T).Name.Split('`')[0];
        var pluralEntityName = entityName.Pluralize();
        
        if (string.IsNullOrWhiteSpace(baseUrl))
            throw new ArgumentNullException(nameof(baseUrl), "Base URL cannot be null or empty.");
        
        Endpoint = baseUrl.EndsWith('/') 
            ? $"{baseUrl}{pluralEntityName}"
            : $"{baseUrl}/{pluralEntityName}";
    }
    
    public async Task<List<T>> GetAllAsync()
    {
        var response = await HttpClient.GetFromJsonAsync<ApiResult<T>>(Endpoint);
        return response?.Items ?? [];
    }
    
    public async Task<T?> GetByIdAsync(string id)
    {
        return await HttpClient.GetFromJsonAsync<T>($"{Endpoint}/{id}");
    }

    public async Task AddAsync(T entity)
    {
        await HttpClient.PostAsJsonAsync(Endpoint, entity);
    }

    public async Task UpdateAsync(string id, T entity)
    {
        await HttpClient.PutAsJsonAsync($"{Endpoint}/{id}", entity);
    }

    public async Task DeleteAsync(object id, bool softDelete = false)
    {
        await HttpClient.DeleteAsync($"{Endpoint}/{id}?softDelete={softDelete}");
    }
    
    public async Task<GridData<T>> LoadServerData(GridState<T> state, string? searchString = null, CancellationToken cancellationToken = default)
    {
        try
        {
            string? sortBy = state.SortDefinitions?.FirstOrDefault()?.SortBy;
            bool sortDesc = state.SortDefinitions?.FirstOrDefault()?.Descending ?? false;

            var queryParams = new Dictionary<string, string?>
            {
                { "page", state.Page.ToString() },
                { "pageSize", state.PageSize.ToString() },
                { "sortBy", sortBy },
                { "sortDesc", sortDesc.ToString().ToLower() },
                { "search", searchString }
            };

            var queryString = string.Join("&", queryParams
                .Where(kv => !string.IsNullOrWhiteSpace(kv.Value))
                .Select(kv => $"{kv.Key}={Uri.EscapeDataString(kv.Value!)}"));

            var response = await HttpClient.GetFromJsonAsync<ApiResult<T>>($"{Endpoint}?{queryString}", cancellationToken);

            return new GridData<T>
            {
                Items = response?.Items ?? new List<T>(),
                TotalItems = response?.TotalItems ?? 0
            };
        }
        catch (OperationCanceledException)
        {
            // La operación fue cancelada; retornar datos vacíos
            return new GridData<T>
            {
                Items = new List<T>(),
                TotalItems = 0
            };
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error al cargar datos: {ex.Message}");
            return new GridData<T>
            {
                Items = new List<T>(),
                TotalItems = 0
            };
        }
    }

    public async Task<IList<T>> FindAsync(string dynamicFilter)
    {
        try
        {
            // Realiza la solicitud al backend con la cadena como predicado
            var response = await HttpClient.GetFromJsonAsync<ApiResult<T>>($"{Endpoint}/find?predicate={Uri.EscapeDataString(dynamicFilter)}");
            return response?.Items ?? [];
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
            throw;
        }
        
    }

    public async Task<bool> ExistsAsync(string property, object value)
    {
        try
        {
            var response = await HttpClient.GetFromJsonAsync<bool>($"{Endpoint}/exists?property={property}&value={Uri.EscapeDataString(value.ToString()!)}");
           
            return response;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }
}