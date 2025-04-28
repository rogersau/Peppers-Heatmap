using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace HeatMapAPI.Helpers
{
    public class EnumSchemaFilter : ISchemaFilter
    {
        public void Apply(OpenApiSchema schema, SchemaFilterContext context)
        {
            if (context.Type.IsEnum)
            {
                schema.Enum.Clear();
                schema.Type = "string";
                schema.Format = null;
                
                foreach (var enumValue in Enum.GetValues(context.Type))
                {
                    var memberInfo = context.Type.GetMember(enumValue.ToString())[0];
                    var displayAttribute = memberInfo.GetCustomAttribute<DisplayAttribute>();
                    
                    // Use display name if available, otherwise use enum value
                    string name = displayAttribute?.Name ?? enumValue.ToString();
                    schema.Enum.Add(new OpenApiString(name));
                }
            }
        }
    }
}
