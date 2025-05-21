using System.Linq.Dynamic.Core;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Mvc;
using STZ.Shared.Dtos;

namespace STZ.Shared.Bases;

[Route("[controller]")]
public class StzControllerBase<T> : ControllerBase where T : class
{
    private readonly ILogger _logger;
    private readonly DbContextBase _dbContext;

    public StzControllerBase(ILogger logger, DbContextBase dbContext)
    {
        _logger = logger;
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] int? page = null,
        [FromQuery] int? pageSize = null,
        [FromQuery] string? search = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] bool? sortDesc = null)
    {
        try
        {
            IQueryable<T> query = _dbContext.Set<T>();

            // Incluir propiedades de navegación (ForeignKeys)
            var navigationProperties = _dbContext.Model.FindEntityType(typeof(T))?
                .GetNavigations()
                .Select(n => n.Name);

            if (navigationProperties != null)
            {
                foreach (var navigationProperty in navigationProperties)
                {
                    query = query.Include(navigationProperty);
                }
            }

            // Filtro por SoftDelete si aplica
            query = ApplySoftDeleteFilter(query);

            // Filtro por búsqueda si se envía
            if (!string.IsNullOrWhiteSpace(search))
            {
                var searchExpression = BuildSearchExpression(search);
                query = query.Where(searchExpression);
            }

            // Ordenamiento si se solicita
            if (!string.IsNullOrWhiteSpace(sortBy))
            {
                var sortExpression = sortDesc == true ? $"{sortBy} descending" : sortBy;
                query = query.OrderBy(sortExpression);
            }

            var totalItems = await query.CountAsync();

            // Si no hay paginación, retorna todos los resultados
            if (page is null || pageSize is null)
            {
                var allItems = await query.ToListAsync();
                return Ok(new { totalItems = allItems.Count, items = allItems });
            }

            // Aplicar paginación
            var pagedItems = await query
                .Skip(page.Value * pageSize.Value)
                .Take(pageSize.Value)
                .ToListAsync();

            return Ok(new { totalItems, items = pagedItems });
        }
        catch (Exception e)
        {
            _logger.LogError(e, e.Message);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("find")]
    public async Task<IActionResult> Find([FromQuery] string predicate)
    {
        try
        {
            var query = _dbContext.Set<T>().Where(predicate);
            query = ApplySoftDeleteFilter(query);
            var response = await query.ToListAsync();
            
            return Ok(new { response.Count, items = response });
        }
        catch (Exception e)
        {
            _logger.LogError(e, e.Message);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(string id)
    {
        try
        {
            object entityId;
            var idPropertyType = typeof(T).GetProperty("Id")?.PropertyType;

            if (idPropertyType == typeof(Guid))
            {
                entityId = Guid.Parse(id);
            }
            else
            {
                entityId = Convert.ChangeType(id, idPropertyType);
            }

            T? result;

            if (typeof(T).IsSubclassOf(typeof(AuditBase<>)))
            {
                var parameter = Expression.Parameter(typeof(T), "x");
                var property = Expression.Property(parameter, "Id");
                var constant = Expression.Constant(entityId);
                var equals = Expression.Equal(property, constant);
                var lambda = Expression.Lambda<Func<T, bool>>(equals, parameter);

                result = await _dbContext.Set<T>()
                    .Where(x => !IsDeleted(x))
                    .FirstOrDefaultAsync(lambda);
            }
            else
            {
                var query = _dbContext.Set<T>().Where(e => EF.Property<object>(e, "Id").Equals(entityId));
                query = ApplySoftDeleteFilter(query);
                result = await query.FirstOrDefaultAsync();
            }

            if (result == null)
            {
                return NotFound();
            }

            return Ok(result);
        }
        catch (Exception e)
        {
            _logger.LogError(e, e.Message);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("exists")]
    public async Task<IActionResult> ExistsAsync([FromQuery] string property, [FromQuery] string value)
    {
        try
        {
            var propInfo = typeof(T).GetProperty(property);
            if (propInfo == null)
                return BadRequest($"La propiedad '{property}' no existe en el tipo '{typeof(T).Name}'.");

            // Convertir el valor al tipo real de la propiedad
            var convertedValue = Convert.ChangeType(value, propInfo.PropertyType);

            var parameter = Expression.Parameter(typeof(T), "x");
            var propertyAccess = Expression.Property(parameter, propInfo);
            var constant = Expression.Constant(convertedValue, propInfo.PropertyType);
            var equals = Expression.Equal(propertyAccess, constant);
            var lambda = Expression.Lambda<Func<T, bool>>(equals, parameter);

            var exists = await _dbContext.Set<T>().AnyAsync(lambda);
            return Ok(exists);
        }
        catch (Exception e)
        {
            _logger.LogError(e, e.Message);
            return StatusCode(500, "Error interno del servidor");
        }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Put([FromRoute] string id, [FromBody] T entity)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var entityId = GetEntityId(entity);

            if (!id.Equals(entityId.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest("El id del objeto no coincide con el id de la URL");
            }

            _dbContext.Entry(entity).State = EntityState.Modified;

            try
            {
                await _dbContext.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                return StatusCode(409, "El registro ya fue modificado por otro usuario");
            }

            return NoContent();
        }
        catch (Exception e)
        {
            _logger.LogError(e, e.Message);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost]
    public async Task<IActionResult> Post([FromBody] T entity)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                Console.WriteLine("ModelState no es válido");
                return BadRequest(ModelState);
            }
            
            _dbContext.Set<T>().Add(entity);
            await _dbContext.SaveChangesAsync();
            await OnAfterPostAsync(entity);

            return CreatedAtAction("Get", new { id = GetEntityId(entity) }, entity);
        }
        catch (Exception e)
        {
            _logger.LogError(e, e.Message);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete([FromRoute] string id, [FromQuery] bool softDelete = false)
    {
        try
        {
            var idProperty = typeof(T).GetProperty("Id");
            if (idProperty == null)
                return BadRequest("La entidad no tiene una propiedad 'Id'.");

            object entityId = idProperty.PropertyType == typeof(Guid)
                ? Guid.Parse(id)
                : Convert.ChangeType(id, idProperty.PropertyType);

            var entity = await _dbContext.Set<T>().FindAsync(entityId);
            if (entity == null)
            {
                return NotFound();
            }

            if (softDelete)
            {
                var isDeletedProperty = typeof(T).GetProperty("IsDeleted");
                if (isDeletedProperty != null && isDeletedProperty.PropertyType == typeof(bool))
                {
                    isDeletedProperty.SetValue(entity, true);
                    _dbContext.Entry(entity).State = EntityState.Modified;
                }
                else
                {
                    return BadRequest("La entidad no soporta eliminación lógica (IsDeleted no existe o no es booleano).");
                }
            }
            else
            {
                _dbContext.EnableSoftDelete = false;
                _dbContext.Set<T>().Remove(entity);
            }

            await _dbContext.SaveChangesAsync();
            return StatusCode(204, "Eliminado correctamente");
        }
        catch (Exception e)
        {
            _logger.LogError(e, e.Message);
            return StatusCode(500, "Internal server error");
        }
    }
    
    protected virtual Task OnAfterPostAsync(T entity)
    {
        return Task.CompletedTask;
    }

    private static object GetEntityId(T entity)
    {
        var propertyInfo = entity.GetType().GetProperty("Id");
        return propertyInfo?.GetValue(entity) ?? throw new InvalidOperationException("Entity does not have an Id property");
    }

    private static bool IsDeleted(T entity)
    {
        var propertyInfo = entity.GetType().GetProperty("IsDeleted");
        if (propertyInfo == null)
        {
            return false;
        }

        var value = propertyInfo.GetValue(entity);
        return value != null && (bool)value;
    }
    
    private Expression<Func<T, bool>> BuildSearchExpression(string search)
    {
        var parameter = Expression.Parameter(typeof(T), "x");
        Expression? combinedExpression = null;

        foreach (var property in typeof(T).GetProperties().Where(p => p.PropertyType == typeof(string)))
        {
            var propertyAccess = Expression.Property(parameter, property);
            var searchExpression = Expression.Call(
                typeof(DbFunctionsExtensions).GetMethod(nameof(DbFunctionsExtensions.Like), new[] { typeof(DbFunctions), typeof(string), typeof(string) })!,
                Expression.Constant(EF.Functions),
                propertyAccess,
                Expression.Constant($"%{search}%"));

            combinedExpression = combinedExpression == null
                ? searchExpression
                : Expression.OrElse(combinedExpression, searchExpression);
        }

        return combinedExpression != null
            ? Expression.Lambda<Func<T, bool>>(combinedExpression, parameter)
            : x => true;
    }
    
    private IQueryable<T> ApplySoftDeleteFilter(IQueryable<T> query)
    {
        var isDeletedProperty = typeof(T).GetProperty("IsDeleted");
        if (isDeletedProperty != null && isDeletedProperty.PropertyType == typeof(bool))
        {
            var parameter = Expression.Parameter(typeof(T), "x");
            var propertyAccess = Expression.Property(parameter, isDeletedProperty);
            var falseConstant = Expression.Constant(false);
            var notDeleted = Expression.Equal(propertyAccess, falseConstant);
            var lambda = Expression.Lambda<Func<T, bool>>(notDeleted, parameter);

            query = query.Where(lambda);
        }

        return query;
    }
}