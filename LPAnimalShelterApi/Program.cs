using Microsoft.AspNetCore.Http.Json;
using Microsoft.OpenApi.Models;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using static MiniValidation.MiniValidator;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.Configure<JsonOptions>(options =>
{
    options.SerializerOptions.WriteIndented = true;
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
builder.Services.ConfigureSwaggerGen(options => options.CustomSchemaIds(t => t.FullName));
builder.Services.AddSwaggerGen(options => 
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Version = "v1",
        Title = "LP Animal Shelter Api",
        Description = "LP No-kill Animal Shelter Api",
        Contact = new OpenApiContact
        {
            Name = "Gaelan Brewer",
            Email = "brewerg82@msn.com",
            Url = new Uri("https://github.com/automachina")
        }
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.UseSwagger();
app.UseSwaggerUI(c => 
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "LP Animal Shelter Api v1");
});

var shelter = new Shelter();

#if DEBUG
// Seeding Debug Data
shelter.TryAddAnimal(new Animal("Dog", "Max", 34.5));
shelter.TryAddAnimal(new Animal("Dog", "Sam", 18.4));
shelter.TryAddAnimal(new Animal("Cat", "Kitty", 8.6));
shelter.TryAddAnimal(new Animal("Dog", "Spot", 56.8));
shelter.TryAddAnimal(new Animal("Dog", "Bella", 22.0));
shelter.TryAddAnimal(new Animal("Cat", "Luna", 11.1));
shelter.TryAddAnimal(new Animal("Cat", "Lily", 13.2));
shelter.TryAddAnimal(new Animal("Cat", "Milo", 4.2));
shelter.TryAddAnimal(new Animal("Dog", "Otis", 12.1));
shelter.TryAddAnimal(new Animal("Pig", "Leo", 78));
shelter.TryAddAnimal(new Animal("Snake", "Chloe", 8.2));
shelter.TryAddAnimal(new Animal("Pony", "Jasper", 92.9));
shelter.TryAddAnimal(new Animal("Ferret", "Lily", 2.1));
for (int i = 1; i <= 10; i++)
    shelter.TryAddAnimal(new Animal("Cat", $"Dizzy{i}", 9.1));
for (int i = 1; i <= 10; i++)
    shelter.TryAddAnimal(new Animal("Dog", $"Bob{i}", 34.5));

#endif
/// <summary>
/// Retrieves the shelter's current kennel statuses
/// </summary>
app.MapGet("/", shelter.GetShelterKennels);

/// <summary>
/// Retrieves the available kennels in the shelter.
/// </summary>
app.MapGet("/available", shelter.GetAvailableKennels);

/// <summary>
/// Reorganizes the animals in the shelter's kennels and return kennels
/// </summary>
app.MapGet("/reorganize", shelter.Reorganize);

/// <summary>
/// Get a specific animal by Id
/// </summary>
app.MapGet("/animal/{id}", (int id) =>
{
    if (shelter.TryGetAnimal(id, out var animal))
        return Results.Ok(animal);

    return Results.NotFound();
}).WithTags(nameof(Animal));

/// <summary>
/// Add an animal to the shelter if there's room.
/// </summary>
app.MapPost("/animal", (Animal animal) => 
{
    if (!TryValidate(animal, out var errors))
        return Results.BadRequest(errors);

    if (shelter.TryAddAnimal(animal, out Animal newAnimal))
        return Results.Ok(newAnimal);

    return Results.Conflict(animal);
}).WithTags(nameof(Animal));

/// <summary>
/// Remove an animal from the shelter.
/// </summary>
app.MapDelete("/animal/{id}", (int id) => 
{
    if (shelter.TryRemoveAnimal(id, out Animal newAnimal))
        return Results.Ok(newAnimal);

    return Results.NotFound(id);
}).WithTags(nameof(Animal));

app.Run();

enum KennelSize
{
    Large,
    Medium,
    Small
}

record Animal
{
    public static Animal Empty => new Animal("","",-1);
    public int Id { get; init; }
    public int KennelId { get; set; }
    [Required]
    public string Type { get; init; }
    [Required]
    public string Name { get; init; }
    [Range(double.Epsilon, double.MaxValue, ErrorMessage = "Animal weight must be greater than zero")]
    public double Weight { get; init; }

    public Animal(string type, string name, double weight)
    {
        Type = type;
        Name = name;
        Weight = weight;
    }
}

class Kennel
{
    public int Id { get; init; }
    public KennelSize Size { get; init; }
    public Animal? Occupant { get; private set; }
    [JsonIgnore]
    public bool IsOccupied => Occupant != null;

    public Kennel(int id, KennelSize size)
    {
        Id = id;
        Size = size;
    }

    public bool CanOccupy(Animal animal)
    {
        if (Occupant != null) return false;
        if (Size == KennelSize.Large) return true;
        if (Size == KennelSize.Medium && animal.Weight <= 50) return true;
        if (Size == KennelSize.Small && animal.Weight <= 20) return true;
        return false;
    }

    public bool TryAddAnimal(Animal animal)
    {
        if (CanOccupy(animal))
        {
            Occupant = animal with { KennelId = Id };
            return true;
        }
        return false;
    }

    public bool TryAddAnimal(Animal animal, out Animal newAnimal)
    {
        newAnimal = animal;
        if (CanOccupy(animal))
        {
            newAnimal = animal with { KennelId = Id };
            Occupant = newAnimal;
            return true;
        }
        return false;
    }

    public bool TryRemoveAnimal(out Animal animal)
    {
        animal = Animal.Empty;
        if (Occupant == null) return false;
        animal = Occupant;
        Occupant = null;
        return true;
    }

    public bool TryRemoveAnimal(int id, out Animal animal)
    {
        animal = Animal.Empty;
        if (Occupant?.Id != id) return false;
        animal = Occupant;
        Occupant = null;
        return true;
    }
}

class Shelter
{
    readonly int largeKennelCount = 8;
    readonly int mediumKennelCount = 10;
    readonly int smallKennelCount = 16;

    readonly Dictionary<int, Kennel> kennels = new Dictionary<int, Kennel>();

    int animalIndex = 0;

    int totalKennels => largeKennelCount + mediumKennelCount + smallKennelCount;

    public Shelter(int largeKennels, int mediumKennels, int smallKennels) : this()
    {
        largeKennelCount = largeKennels;
        mediumKennelCount = mediumKennels;
        smallKennelCount = smallKennels;
    }

    public Shelter()
    { 
        for(int i = 1; i <= totalKennels; i++)
        {
            if (i <= smallKennelCount)
                kennels[i] = new Kennel(i, KennelSize.Small);
            else if (i <= smallKennelCount + mediumKennelCount)
                kennels[i] = new Kennel(i, KennelSize.Medium);
            else 
                kennels[i] = new Kennel(i, KennelSize.Large);
        }
    }

    public bool TryAddAnimal(Animal animal)
    {
        for (var i = 1; i <= kennels.Count; i++)
        {
            var stall = kennels[i];
            if (stall.TryAddAnimal(animal with { Id = animalIndex }))
            {
                animalIndex++;
                return true;
            }
        }
        return false;
    }

    public bool TryAddAnimal(Animal animal, out Animal newAnimal)
    {
        newAnimal = Animal.Empty;
        for (var i = 1; i <= kennels.Count; i++)
        {
            var stall = kennels[i];
            if(stall.TryAddAnimal(animal with { Id = animalIndex }, out newAnimal))
            {
                animalIndex++;
                return true;
            }
        }
        return false;
    }

    public bool TryGetAnimal(int id, out Animal animal)
    {
        animal = Animal.Empty;
        for (var i = 1; i <= kennels.Count; i++)
        {
            var stall = kennels[i];
            if (stall.Occupant?.Id == id)
            {
                animal = stall.Occupant;
                return true;
            }
        }
        return false;
    }

    public bool TryRemoveAnimal(int id, out Animal animal)
    {
        animal = Animal.Empty;
        for (var i = 1; i <= kennels.Count; i++)
        {
            var stall = kennels[i];
            if(stall.TryRemoveAnimal(id, out animal))
                return true;
        }
        return false;
    }

    public IList<Kennel> GetShelterKennels() => 
        kennels.Select(k => k.Value).ToList();

    public IList<Kennel> GetAvailableKennels() =>
        kennels.Where(k => !k.Value.IsOccupied).Select(k => k.Value).ToList();

    public bool TryMoveAnimal(Kennel source, Kennel target)
    {
        if (source == null || !source.IsOccupied || target == null || target.IsOccupied)
            return false;
        if (source.TryRemoveAnimal(out var removed) && target.TryAddAnimal(removed))
            return true;
        source.TryAddAnimal(removed);
        return false;
    }

    public IList<Kennel> Reorganize()
    {
        for(var i = kennels.Count; i > 0; i--)
        {
            var source = kennels[i];
            if (!(source.Occupant is Animal occupant)) 
                continue;
            for(var j = 1; j < i; j++)
            {
                var target = kennels[j];
                if(target.CanOccupy(occupant))
                    TryMoveAnimal(source, target);
            }
        }
        return GetShelterKennels();
    }
}