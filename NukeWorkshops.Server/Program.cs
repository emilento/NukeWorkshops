var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseResponseCompression();
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseDefaultFiles();
app.UseStaticFiles();

app.UseRouting();
app.UseHttpsRedirection();
app.MapDefaultControllerRoute();

app.Run();
