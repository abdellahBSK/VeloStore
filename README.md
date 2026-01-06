# 🛒 VeloStore — E-Commerce Platform

VeloStore is a **professional e-commerce web application**, built with **ASP.NET Core Razor Pages**, following **MVVM architecture**, using **Entity Framework Core** and **SQL Server**.

This project is designed as a **real-world, scalable e-commerce system**, suitable for **portfolio, PFE, and professional demonstration**.

---

## ✨ Features

### 🏠 Public Area
- Home page with product catalog
- Product details page
- Search, filters (price, name), and sorting
- Responsive and modern UI (Amazon-like)
- Bootstrap 5 styling

### 🛒 Shopping Cart
- Add products to cart
- Increase / decrease quantities
- Remove products
- Session-based cart (Amazon-style behavior)
- Automatic total calculation

### 👤 Authentication
- User registration (Register)
- User login / logout
- Secure authentication using **ASP.NET Core Identity**
- Users stored in SQL Server

### 🧑‍💼 Admin (Planned)
- Product management (CRUD)
- Product image upload
- Order management
- Role-based access (Admin / Client)

---

## 🧠 Architecture

```
VeloStore
│
├── Models          # Database entities
├── ViewModels      # Data passed to the views (MVVM)
├── Data            # DbContext, migrations, seed
├── Services        # Business logic (Cart, etc.)
├── Pages           # Razor Pages (UI)
│   ├── Product
│   ├── Cart
│   ├── Shared
├── wwwroot         # Static assets (CSS, images)
└── Program.cs
```

---

## 🛠️ Tech Stack

- ASP.NET Core 8
- Razor Pages
- MVVM Architecture
- Entity Framework Core
- SQL Server
- ASP.NET Core Identity
- Bootstrap 5
- Git & GitHub

---

## 🗄️ Database

- SQL Server
- Code-First approach (EF Core Migrations)
- Seeded data for fast development
- Product images via placeholder URLs (Picsum)

---

## ⚙️ Getting Started

### 1️⃣ Clone the repository
```bash
git clone https://github.com/abdellahBSK/VeloStore.git
```

### 2️⃣ Configure the database
Update the connection string in `appsettings.json`:

```json
"ConnectionStrings": {
  "DefaultConnection": "Server=.;Database=VeloStoreDB;Trusted_Connection=True;TrustServerCertificate=True"
}
```

### 3️⃣ Apply migrations
```powershell
Update-Database
```

### 4️⃣ Run the project
```bash
dotnet run
```

---

## 📌 Project Status

✅ Product catalog  
✅ Product details  
✅ Shopping cart  
✅ Search & filters  
✅ Authentication (Login / Register)  
🔄 Admin dashboard (in progress)  
🔄 Checkout & orders (planned)  
🔄 Deployment (planned)

---

## 🎯 Learning Objectives

- Master ASP.NET Core Razor Pages
- Apply MVVM architecture
- Work with Entity Framework Core & SQL Server
- Build a real-world e-commerce system
- Practice clean code and Git workflow

---

## 👨‍💻 Author

**Abdellah Bouskri**  
Software Engineering Student  

GitHub: https://github.com/abdellahBSK

---

## 📄 License

This project is intended for **educational and portfolio purposes**.
