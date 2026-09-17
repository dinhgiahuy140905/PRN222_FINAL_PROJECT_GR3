# PRN222 Sales Management — Project Review

## 1. Project Overview

**Project:** PRN222_SalesManagement  
**Application:** SalesManagement.Web  
**Type:** ASP.NET Core MVC Web Application  
**Language:** C#  
**Framework:** .NET 8.0 LTS  
**IDE:** Visual Studio 2022  
**Course context:** PRN222 — Programming with .NET

The project is a lightweight multi-store sales management system intended for a 3-person student team. The priority is to keep implementation practical and manageable while preserving a structure that can be extended later.

---

## 2. Current Direction

The application is a web-based sales management system.

A user logs into the system and can participate in one or more stores. A user's role is determined per store rather than globally.

Example:

- User A can be Owner of Store 1.
- The same User A can be Owner of Store 2.
- The same User A can also be Staff of Store 3.

Therefore, `Role` must NOT be stored directly on `User`.

The relationship is modeled through `StoreMember`.

```text
User
  |
  | 1
  v *
StoreMember
  |
  | *
  v 1
Store
```

Conceptually:

```text
User ----< StoreMember >---- Store
                              |
                              +----< Category
                              |
                              +----< Product
                              |
                              +----< Sale
                                      |
                                      +----< SaleDetail
```

---

## 3. Roles

### Owner

Expected core capabilities:

- Create/manage stores.
- Manage store information.
- Add or manage staff.
- Manage categories.
- Manage products.
- Create/manage sales.
- View store sales information/reports.

### Staff

Expected core capabilities:

- Access stores where the user is a member.
- View products/categories.
- Create sales/orders.
- View information allowed by the store role.

Keep authorization simple for the course project. More detailed permissions can be added later only if necessary.

---

## 4. Core Entities

Planned core entities:

```text
User
Store
StoreMember
Category
Product
Sale
SaleDetail
```

### User

Represents an application account.

Current model:

```csharp
public class User
{
    public int Id { get; set; }

    public string FullName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
```

Important rules:

- Do not store plaintext passwords.
- Do not put `Role` directly on `User`.
- Do not put a single `StoreId` directly on `User`.

### Store

Represents a store in the system.

Planned model:

```csharp
public class Store
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Address { get; set; }

    public string? Phone { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
```

### StoreMember

Will represent membership between a User and a Store.

Expected conceptual fields:

```text
Id
UserId
StoreId
Role (Owner / Staff)
```

This entity is important because role/authorization is scoped to a store.

---

## 5. Planned Technology Stack

Keep the stack straightforward:

- ASP.NET Core MVC
- .NET 8
- C#
- Razor Views
- Entity Framework Core
- SQL Server
- ASP.NET Core authentication/authorization approach appropriate for the project
- Bootstrap/default MVC frontend initially

Avoid adding unnecessary architecture or frontend frameworks unless requirements later justify them.

In particular, do not introduce React/Angular/Vue or split the application into a separate frontend and Web API without a clear requirement.

---

## 6. Current Project Structure

Current solution:

```text
PRN222_SalesManagement/
|
+-- PRN222_SalesManagement.sln
|
+-- SalesManagement.Web/
    |
    +-- Controllers/
    +-- Models/
    +-- Views/
    +-- wwwroot/
    +-- appsettings.json
    +-- Program.cs
```

MVC responsibilities:

```text
Controller -> handles requests/application flow
Model      -> application/domain/data models
View       -> Razor UI
wwwroot    -> static CSS/JS/images
Program.cs -> application/service/middleware configuration
```

---

## 7. Current Program.cs

Initial ASP.NET Core MVC configuration:

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
```

Authentication has NOT been configured yet.

When authentication is implemented later, the middleware pipeline will need to be reviewed accordingly.

---

## 8. Current Progress

Completed:

- Visual Studio 2022 environment configured.
- ASP.NET/Web Development workload installed.
- ASP.NET Core MVC template available.
- Solution `PRN222_SalesManagement` created.
- Project `SalesManagement.Web` created.
- .NET 8.0 LTS selected.
- HTTPS enabled.
- Authentication initially set to `None`.
- Default MVC project successfully runs.
- Initial architecture reviewed.
- Core entity relationships identified.
- `User.cs` created.

Current/next model work:

- `Store.cs`
- `StoreMember.cs`
- Remaining business entities after the core user/store relationship is established.

---

## 9. Development Rules for Codex

When working on this repository, follow these rules:

1. Work incrementally. Make one logical change at a time.
2. Do not generate the entire application at once.
3. Before making a major architectural change, explain why it is needed.
4. Prefer simple ASP.NET Core MVC solutions suitable for a PRN222 student project.
5. Do not over-engineer the project with unnecessary repositories, microservices, CQRS, event buses, etc.
6. Preserve the multi-store membership design.
7. A user's Owner/Staff role belongs to `StoreMember`, not `User`.
8. A user may belong to multiple stores.
9. Do not store passwords in plaintext.
10. Use async database operations where appropriate once EF Core is introduced.
11. Keep controllers reasonably thin; move substantial business logic into services when it becomes necessary.
12. Validate changes by building the project after meaningful code modifications.
13. Do not silently change the selected technology stack.
14. If requirements are ambiguous, prefer the simplest implementation that satisfies the existing project design.

---

## 10. Scope Control

This is a course group project with only three members and limited development time.

Priority order:

```text
Correct core flow
    >
Simple maintainable implementation
    >
Required course concepts
    >
Nice-to-have features
    >
Complex scalability architecture
```

The system should be capable of future extension, but scalability should not come at the cost of unnecessary implementation complexity now.

---

## 11. Immediate Next Direction

Continue from the core domain model.

Recommended sequence:

```text
User
  -> Store
  -> StoreMember
  -> EF Core / DbContext
  -> SQL Server connection
  -> Migration
  -> Authentication
  -> Store selection / authorization
  -> Product & Category
  -> Sales
```

Do not jump ahead through this sequence without a reason.

---

## 12. Status Note for Future AI Sessions

The project is still in the initial setup/domain-design stage.

Do not assume that database configuration, authentication, EF Core migrations, StoreMember, products, or sales features have already been implemented. Inspect the repository first and use the actual code as the source of truth.

This document describes the intended direction; when it conflicts with existing working source code or newly confirmed requirements, review the discrepancy before changing the implementation.
