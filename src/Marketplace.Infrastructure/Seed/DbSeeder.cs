using Marketplace.Core.Entities;
using Marketplace.Core.Enums;
using Marketplace.Core.Utils;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Marketplace.Infrastructure.Persistence;

namespace Marketplace.Infrastructure.Seed;

public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext db, UserManager<AppUser> userManager, RoleManager<IdentityRole<Guid>> roleManager)
    {
        if (await db.Categories.AnyAsync())
            return;

        var roles = new[] { UserRole.Admin, UserRole.Seller, UserRole.Buyer };
        foreach (var roleName in roles)
        {
            var existingRole = await roleManager.FindByNameAsync(roleName);
            if (existingRole is null)
            {
                await roleManager.CreateAsync(new IdentityRole<Guid>(roleName));
            }
        }

        var admin = new AppUser
        {
            Email = "admin@promka.ua",
            UserName = "admin@promka.ua",
            DisplayName = "Адмін",
            EmailConfirmed = true,
            IsApproved = true
        };
        var result = await userManager.CreateAsync(admin, "Admin123!");
        if (result.Succeeded)
            await userManager.AddToRoleAsync(admin, UserRole.Admin);

        var sellers = new List<AppUser>
        {
            new() { Email = "sport@promka.ua", UserName = "sport@promka.ua", DisplayName = "Sport Style UA", EmailConfirmed = true, IsApproved = true },
            new() { Email = "adidas@promka.ua", UserName = "adidas@promka.ua", DisplayName = "Adidas Originals Ukraine", EmailConfirmed = true, IsApproved = true },
            new() { Email = "techshop@promka.ua", UserName = "techshop@promka.ua", DisplayName = "TechShop Ukraine", EmailConfirmed = true, IsApproved = true },
            new() { Email = "apple@promka.ua", UserName = "apple@promka.ua", DisplayName = "Apple Kyiv", EmailConfirmed = true, IsApproved = true },
            new() { Email = "fashion@promka.ua", UserName = "fashion@promka.ua", DisplayName = "UkrStyle Fashion", EmailConfirmed = true, IsApproved = true },
            new() { Email = "timestore@promka.ua", UserName = "timestore@promka.ua", DisplayName = "TimeStore UA", EmailConfirmed = true, IsApproved = true },
            new() { Email = "bagshop@promka.ua", UserName = "bagshop@promka.ua", DisplayName = "Bag UA", EmailConfirmed = true, IsApproved = true },
            new() { Email = "laptop@promka.ua", UserName = "laptop@promka.ua", DisplayName = "Electronic Parts", EmailConfirmed = true, IsApproved = true }
        };

        foreach (var seller in sellers)
        {
            result = await userManager.CreateAsync(seller, "Seller123!");
            if (result.Succeeded)
                await userManager.AddToRoleAsync(seller, UserRole.Seller);
        }

        var buyers = new List<AppUser>
        {
            new() { Email = "user@promka.ua", UserName = "user@promka.ua", DisplayName = "Олександр Петренко", EmailConfirmed = true, IsApproved = true },
            new() { Email = "mariya@promka.ua", UserName = "mariya@promka.ua", DisplayName = "Марія Коваленко", EmailConfirmed = true, IsApproved = true },
            new() { Email = "andriy@promka.ua", UserName = "andriy@promka.ua", DisplayName = "Андрій Шевченко", EmailConfirmed = true, IsApproved = true },
            new() { Email = "olga@promka.ua", UserName = "olga@promka.ua", DisplayName = "Ольга Бондаренко", EmailConfirmed = true, IsApproved = true },
            new() { Email = "ivan@promka.ua", UserName = "ivan@promka.ua", DisplayName = "Іван Ковач", EmailConfirmed = true, IsApproved = true },
            new() { Email = "anna@promka.ua", UserName = "anna@promka.ua", DisplayName = "Анна Мороз", EmailConfirmed = true, IsApproved = true },
            new() { Email = "maxim@promka.ua", UserName = "maxim@promka.ua", DisplayName = "Максим Козак", EmailConfirmed = true, IsApproved = true },
            new() { Email = "julia@promka.ua", UserName = "julia@promka.ua", DisplayName = "Юлія Вільхова", EmailConfirmed = true, IsApproved = true },
            new() { Email = "serhiy@promka.ua", UserName = "serhiy@promka.ua", DisplayName = "Сергій Дуб", EmailConfirmed = true, IsApproved = true }
        };

        foreach (var buyer in buyers)
        {
            result = await userManager.CreateAsync(buyer, "Buyer123!");
            if (result.Succeeded)
                await userManager.AddToRoleAsync(buyer, UserRole.Buyer);
        }

        var electronics = new Category { Name = "Електроніка", Slug = "elektronika", Description = "Телефони, ноутбуки, планшети" };
        var shoesCategory = new Category { Name = "Взуття", Slug = "vzuttya", Description = "Кросівки, кеди, бутси" };
        var apparelCategory = new Category { Name = "Одяг", Slug = "odyag", Description = "Худи, футболки, штани" };
        var accessoriesCategory = new Category { Name = "Аксесуари", Slug = "aksesuary", Description = "Рюкзаки, шапки, годинники" };

        db.Categories.AddRange(electronics, shoesCategory, apparelCategory, accessoriesCategory);
        await db.SaveChangesAsync();

        var tablets = new Category { Name = "Планшети", Slug = "planshety", Description = "Планшети та аксесуари", ParentId = electronics.Id };
        var phones = new Category { Name = "Телефони", Slug = "telefony", Description = "Смартфони", ParentId = electronics.Id };
        var laptops = new Category { Name = "Ноутбуки", Slug = "noutbuki", Description = "Ноутбуки та аксесуари", ParentId = electronics.Id };
        var sneakers = new Category { Name = "Кросівки", Slug = "krosivky", Description = "Спортивні кросівки", ParentId = shoesCategory.Id };
        var boots = new Category { Name = "Черевики", Slug = "cherevyky", Description = "Черевики та бутси", ParentId = shoesCategory.Id };
        var shoes = new Category { Name = "Туфлі", Slug = "tufli", Description = "Туфлі та лофери", ParentId = shoesCategory.Id };
        var hoodies = new Category { Name = "Худи", Slug = "hudi", Description = "Спортивні худи", ParentId = apparelCategory.Id };
        var tshirts = new Category { Name = "Футболки", Slug = "futbolky", Description = "Футболки та майки", ParentId = apparelCategory.Id };
        var pants = new Category { Name = "Штани", Slug = "shtany", Description = "Штани та шорти", ParentId = apparelCategory.Id };
        var jackets = new Category { Name = "Куртки", Slug = "kurtky", Description = "Куртки та пальто", ParentId = apparelCategory.Id };
        var bags = new Category { Name = "Рюкзаки", Slug = "rukzaky", Description = "Рюкзаки та сумки", ParentId = accessoriesCategory.Id };
        var watches = new Category { Name = "Годинники", Slug = "godynnyky", Description = "Годинники та браслети", ParentId = accessoriesCategory.Id };
        var sunglasses = new Category { Name = "Окуляри", Slug = "okulyary", Description = "Сонячні окуляри", ParentId = accessoriesCategory.Id };

        db.Categories.AddRange(tablets, phones, laptops, sneakers, boots, shoes, hoodies, tshirts, pants, jackets, bags, watches, sunglasses);
        await db.SaveChangesAsync();

        var stores = new List<Store>
        {
            new() { OwnerUserId = sellers[0].Id, Name = "Sport Style UA", Slug = "sport-style-ua", Description = "Найкращі спортивні кросівки в Україні", ContactEmail = sellers[0].Email, ContactPhone = "+380671234567", Region = "Київська", City = "Київ", Street = "Хрещатик 15", PostalCode = "01001", IsApproved = true, IsFeatured = true, ApprovedAtUtc = DateTime.UtcNow, ApprovedByUserId = admin.Id, MetaTitle = "Sport Style UA - Кросівки", MetaDescription = "Купити спортивні кросівки в Києві" },
            new() { OwnerUserId = sellers[1].Id, Name = "Adidas Originals Ukraine", Slug = "adidas-originals-ua", Description = "Оригінальний одяг та взуття Adidas", ContactEmail = sellers[1].Email, ContactPhone = "+380671234568", Region = "Київська", City = "Київ", Street = "Площа Льва Толстого 5", PostalCode = "01004", IsApproved = true, IsFeatured = true, ApprovedAtUtc = DateTime.UtcNow, ApprovedByUserId = admin.Id, MetaTitle = "Adidas Originals", MetaDescription = "Оригінальний Adidas в Україні" },
            new() { OwnerUserId = sellers[2].Id, Name = "TechShop Ukraine", Slug = "techshop-ua", Description = "Найновіші смартфони та гаджети", ContactEmail = sellers[2].Email, ContactPhone = "+380671234569", Region = "Львівська", City = "Львів", Street = "Вул. Городоцька 42", PostalCode = "79000", IsApproved = true, ApprovedAtUtc = DateTime.UtcNow, ApprovedByUserId = admin.Id },
            new() { OwnerUserId = sellers[3].Id, Name = "Apple Kyiv", Slug = "apple-kyiv", Description = "Офіційна продукція Apple в Україні", ContactEmail = sellers[3].Email, ContactPhone = "+380671234570", Region = "Київська", City = "Київ", Street = "Площа Дружби Народів 1", PostalCode = "01042", IsApproved = true, IsFeatured = true, ApprovedAtUtc = DateTime.UtcNow, ApprovedByUserId = admin.Id },
            new() { OwnerUserId = sellers[4].Id, Name = "UkrStyle Fashion", Slug = "ukrstyle-fashion", Description = "Сучасний одяг українських та європейських брендів", ContactEmail = sellers[4].Email, ContactPhone = "+380671234571", Region = "Одеська", City = "Одеса", Street = "Дерибасівська 1", PostalCode = "65000", IsApproved = true, ApprovedAtUtc = DateTime.UtcNow, ApprovedByUserId = admin.Id },
            new() { OwnerUserId = sellers[5].Id, Name = "TimeStore UA", Slug = "timestore-ua", Description = "Годинники провідних світових брендів", ContactEmail = sellers[5].Email, ContactPhone = "+380671234572", Region = "Київська", City = "Київ", Street = "Вул. Басейна 2", PostalCode = "01004", IsApproved = true, ApprovedAtUtc = DateTime.UtcNow, ApprovedByUserId = admin.Id },
            new() { OwnerUserId = sellers[6].Id, Name = "Bag UA", Slug = "bag-ua", Description = "Рюкзаки, сумки та аксесуари", ContactEmail = sellers[6].Email, ContactPhone = "+380671234573", Region = "Харківська", City = "Харків", Street = "Вул. Сумська 12", PostalCode = "61000", IsApproved = true, ApprovedAtUtc = DateTime.UtcNow, ApprovedByUserId = admin.Id },
            new() { OwnerUserId = sellers[7].Id, Name = "Electronic Parts", Slug = "electronic-parts", Description = "Ноутбуки та комп'ютерна техніка", ContactEmail = sellers[7].Email, ContactPhone = "+380671234574", Region = "Дніпропетровська", City = "Дніпро", Street = "Вул. Дмитра Яворницького 72", PostalCode = "49000", IsApproved = true, ApprovedAtUtc = DateTime.UtcNow, ApprovedByUserId = admin.Id }
        };

        db.Stores.AddRange(stores);
        await db.SaveChangesAsync();

        var allProducts = new List<Product>();
        var productId = 1;

        void AddProduct(Guid storeId, Guid categoryId, string title, string description, decimal price, int stock)
        {
            var slug = title.ToLowerInvariant()
                .Replace(" ", "-")
                .Replace("'", "")
                .Replace("(", "")
                .Replace(")", "")
                .Replace("/", "-")
                .Replace(",", "");
            slug = slug.Length > 50 ? slug[..50] : slug;
            slug = $"{slug}-{productId++}";

            allProducts.Add(new Product
            {
                StoreId = storeId,
                CategoryId = categoryId,
                Title = title,
                Slug = slug,
                Description = description,
                Price = price,
                Stock = stock,
                IsPublished = true,
                Status = ProductStatus.Approved,
                ApprovedAtUtc = DateTime.UtcNow,
                ApprovedByUserId = admin.Id
            });
        }

        var sportStore = stores[0];
        var adidasStore = stores[1];
        var techStore = stores[2];
        var appleStore = stores[3];
        var fashionStore = stores[4];
        var timeStore = stores[5];
        var bagStore = stores[6];
        var laptopStore = stores[7];

        foreach (var (name, price) in new[] { ("Nike Air Force 1 '07", 4799m), ("Nike Air Max 90", 5499m), ("Nike Dunk Low", 4299m), ("Nike Air Jordan 1", 7999m), ("Nike Blazer Mid '77", 3999m), ("Nike React Infinity Run", 5999m), ("Nike Pegasus 40", 4499m), ("Nike Metcon 9", 5299m), ("Nike Air Zoom Vomero", 4899m), ("Nike Flex Control", 2799m), ("Nike Air Monarch IV", 3299m), ("Nike Court Legacy", 2499m), ("Nike SB Check", 2199m), ("Nike SB Zoom", 3999m), ("Nike Free Run", 3799m), ("Nike Quest 5", 2999m), ("Nike Winflo 10", 3499m), ("Nike Downshifter 12", 2699m), ("Nike Revolution 6", 2299m), ("Nike Air Max 97", 6499m), ("Nike Air Max 270", 4999m), ("Nike Air Max 2023", 6999m), ("Nike Air Max 1", 5499m), ("Nike Air Max 95", 5999m), ("Nike Air Max Plus", 6499m) })
            AddProduct(sportStore.Id, sneakers.Id, name, $"Якісні {name}. Ідеальний вибір для спорту та повсякденного носіння.", price, new Random().Next(5, 20));

        foreach (var (name, price) in new[] { ("Adidas Ultraboost 23", 6999m), ("Adidas Stan Smith", 3299m), ("Adidas Superstar", 3499m), ("Adidas Forum Low", 4299m), ("Adidas Samba OG", 3799m), ("Adidas Gazelle", 3999m), ("Adidas Campus 00s", 3799m), ("Adidas Response CL", 4499m), ("Adidas ZX 8000", 4999m), ("Adidas Yeezy Boost 350 V2", 8999m), ("Adidas Yeezy 500", 8499m), ("Adidas Yeezy Foam Runner", 5499m), ("Adidas Terrex Free Hiker", 7999m), ("Adidas Duramo SL", 2499m), ("Adidas Runfalcon 5", 2699m), ("Adidas Galaxy 6", 2299m), ("Adidas Lite Racer 3.0", 1999m), ("Adidas Breakpoint", 2799m), ("Adidas Nizza", 2499m), ("Adidas Rivalry Low", 3299m), ("Adidas Country OG", 3699m), ("Adidas Handball Spezial", 3999m), ("Adidas Grand Court", 2499m), ("Adidas VL Court", 1999m), ("Adidas Strutter", 2299m) })
            AddProduct(adidasStore.Id, sneakers.Id, name, $"Оригінальні {name} від Adidas. Відмінна якість та стиль.", price, new Random().Next(3, 15));

        foreach (var (name, price) in new[] { ("iPhone 15 Pro Max 256GB", 54999m), ("iPhone 15 Pro 256GB", 47999m), ("iPhone 15 128GB", 36999m), ("iPhone 14 Pro Max 256GB", 42999m), ("iPhone 14 128GB", 29999m), ("Samsung Galaxy S24 Ultra 512GB", 49999m), ("Samsung Galaxy S24+ 256GB", 38999m), ("Samsung Galaxy S24 128GB", 32999m), ("Samsung Galaxy Z Fold5 512GB", 69999m), ("Samsung Galaxy Z Flip5 256GB", 35999m), ("Xiaomi 14 Pro 512GB", 32999m), ("Xiaomi 13T Pro 256GB", 24999m), ("Redmi Note 13 Pro+ 256GB", 15999m), ("Realme GT5 Pro 256GB", 27999m), ("OnePlus 12 256GB", 37999m), ("OPPO Find X7 Pro 256GB", 34999m), ("Huawei P70 Pro 512GB", 32999m), ("Honor Magic6 Pro 512GB", 36999m), ("Google Pixel 8 Pro 256GB", 35999m), ("Nothing Phone 2 256GB", 26999m) })
            AddProduct(techStore.Id, phones.Id, name, $"Сучасний смартфон {name}. Гарантія 12 місяців.", price, new Random().Next(2, 10));

        foreach (var (name, price) in new[] { ("MacBook Pro 14 M3 Pro", 89999m), ("MacBook Pro 16 M3 Max", 149999m), ("MacBook Air 13 M3", 52999m), ("MacBook Air 15 M3", 60999m), ("iPad Pro 12.9 M4 256GB", 54999m), ("iPad Pro 11 M4 256GB", 42999m), ("iPad Air 11 M2 128GB", 27999m), ("iPad mini 6 64GB", 21999m), ("Samsung Galaxy Tab S9 Ultra 512GB", 49999m), ("Samsung Galaxy Tab S9+ 256GB", 34999m), ("Samsung Galaxy Tab S9 128GB", 26999m), ("Xiaomi Pad 6 Pro 256GB", 18999m), ("Lenovo Tab P12 Pro 256GB", 22999m), ("Huawei MatePad Pro 256GB", 24999m), ("Microsoft Surface Pro 9 256GB", 45999m) })
            AddProduct(appleStore.Id, tablets.Id, name, $"Професійний {name}. Потужний процесор, Retina дисплей.", price, new Random().Next(1, 8));

        foreach (var (name, price) in new[] { ("HP Pavilion 15", 32999m), ("Dell XPS 15", 79999m), ("Lenovo ThinkPad X1 Carbon", 69999m), ("ASUS ROG Strix G16", 74999m), ("Acer Predator Helios 18", 89999m), ("MSI Katana 15", 59999m), ("Samsung Galaxy Book4 Pro 360", 54999m), ("Huawei MateBook 14", 42999m), ("Xiaomi RedmiBook Pro 15", 37999m), ("Lenovo IdeaPad Gaming 3", 44999m) })
            AddProduct(laptopStore.Id, laptops.Id, name, $"Потужний ноутбук {name} для роботи та ігор.", price, new Random().Next(2, 8));

        foreach (var (name, price) in new[] { ("Худі Nike Sportswear", 2999m), ("Худі Adidas Originals", 3499m), ("Худі Puma Graphic", 2499m), ("Худі New Balance", 2799m), ("Худі Under Armour", 3199m), ("Худі Reebok", 2699m), ("Худі Kappa", 1999m), ("Худі FILA", 2299m), ("Худі Everlast", 1799m), ("Худі Venum", 2499m), ("Худі Lonsdale", 1899m), ("Худі Everlast MMA", 2299m), ("Худі Bad Boy", 2599m), ("Худі Green Hill", 1699m), ("Худі Twins", 1899m) })
            AddProduct(fashionStore.Id, hoodies.Id, name, $"Стильне {name}. 100% бавовна, зручний крій.", price, new Random().Next(10, 30));

        foreach (var (name, price) in new[] { ("FIFA 24", 2499m), ("iPhone 15 Pro", 4299m), ("UkrStyle", 799m), ("Nike Dri-FIT", 1199m), ("Adidas Climalite", 999m), ("Puma Dry", 899m), ("New Balance Tech", 1099m), ("Under Armour Tech", 1299m), ("Reebok Vector", 799m), ("Kappa Basic", 699m), ("Champion Classic", 899m), ("Carhartt WIP", 1899m), ("Stussy Basic", 1499m), ("Tommy Hilfiger", 2299m), ("Ralph Lauren", 2499m) })
            AddProduct(fashionStore.Id, tshirts.Id, name, $"Сучасна {name}. Якісна тканина, різні кольори.", price, new Random().Next(15, 50));

        foreach (var (name, price) in new[] { ("Джинси Levi's 501", 3499m), ("Джинси Wrangler", 2499m), ("Штани Nike Tech", 2999m), ("Штани Adidas Tiro", 2499m), ("Спортивні штани Puma", 1999m), ("Штани Reebok Workout", 1799m), ("Шорти Nike Flex", 1499m), ("Спортивні шорти Under Armour", 1299m), ("Брюки Tom Tailor", 2799m), ("Чінос Jack & Jones", 1999m) })
            AddProduct(fashionStore.Id, pants.Id, name, $"Зручні {name} для щоденного носіння.", price, new Random().Next(8, 25));

        foreach (var (name, price) in new[] { ("Куртка Nike Academy", 3999m), ("Куртка Adidas Winter", 4999m), ("Вітровка Puma", 2999m), ("Куртка North Face", 8999m), ("Парка Columbia", 5999m), ("Куртка Kappa", 3499m), ("Вітровка Reebok", 2799m), ("Куртка Lacoste", 6999m), ("Пуховик Moncler", 24999m), ("Куртка Burberry", 19999m) })
            AddProduct(fashionStore.Id, jackets.Id, name, $"Тепла {name} для холодної пори року.", price, new Random().Next(3, 15));

        foreach (var (name, price) in new[] { ("Casio G-Shock DW5600", 2299m), ("Casio Edifice EFR-526", 3499m), ("Casio Pro Trek PRW-6900", 4999m), ("Samsung Galaxy Watch 6", 8999m), ("Apple Watch Series 9", 14499m), ("Apple Watch Ultra 2", 24999m), ("Garmin Fenix 7", 17999m), ("Garmin Forerunner 965", 15999m), ("Amazfit GTR 4", 3999m), ("Amazfit T-Rex 2", 3299m), ("Huawei Watch GT 4", 5999m), ("Xiaomi Watch S1", 4499m), ("Fossil Gen 6", 6999m), ("Michael Kors Bradshaw", 8999m), ("Emporio Armani Smartwatch", 11999m), ("Polar Vantage V3", 12999m), ("Suunto 9 Peak Pro", 11999m), ("Garmin Venu 3", 9999m), ("Apple Watch SE", 8999m), ("Samsung Galaxy Watch 5", 5999m), ("Garmin Instinct 2", 5999m), ("Casio Baby-G", 1899m), ("Casio Sheen", 2999m), ("Daniel Wellington Classic", 2499m), ("MVMT Odyssey", 2799m) })
            AddProduct(timeStore.Id, watches.Id, name, $"Стильний {name}. Водостійкість, різні режими.", price, new Random().Next(2, 10));

        foreach (var (name, price) in new[] { ("Рюкзак Nike Elemental", 1899m), ("Рюкзак Adidas Classic", 1699m), ("Рюкзак Puma Back", 1499m), ("Рюкзак JanSport", 2299m), ("Рюкзак Herschel Supply", 2499m), ("Рюкзак Eastpak", 1999m), ("Рюкзак Fjällräven Kånken", 2799m), ("Рюкзак Nike Heritage", 1899m), ("Рюкзак Under Armour", 2199m), ("Рюкзак Adidas 3S", 1299m), ("Сумка Nike Utility", 1499m), ("Сумка Adidas Bar", 1199m), ("Спортивна сумка Puma", 999m), ("Дорожня сумка Samsonite", 3999m), ("Рюкзак Lenovo", 1799m), ("Рюкзак Xiaomi", 1299m), ("Рюкзак Samsung", 1599m), ("Сумка через плече Hugo Boss", 4999m), ("Рюкзак Carhartt", 3499m), ("Рюкзак Stüssy", 2999m), ("Рюкзак Champion", 2499m), ("Сумка Nike Brasilia", 1799m), ("Рюкзак Osprey", 4499m), ("Рюкзак Gregory", 3999m), ("Рюкзак Deuter", 3499m) })
            AddProduct(bagStore.Id, bags.Id, name, $"Міцний {name}. Ідеальний для щоденного використання.", price, new Random().Next(5, 20));

        foreach (var (name, price) in new[] { ("Oakley Holbrook", 2499m), ("Ray-Ban Aviator", 3299m), ("Ray-Ban Wayfarer", 2999m), ("Oakley Frogskin", 2199m), ("Persol PO3019S", 4499m), ("Prada Linea Rossa", 6999m), ("Gucci GG0061S", 8999m), ("Dior Black Suit", 7999m), ("Cartier CT0025S", 5999m), ("Miumiu MU02RS", 5499m) })
            AddProduct(bagStore.Id, sunglasses.Id, name, $"Оригінальні {name}. 100% захист від UV.", price, new Random().Next(2, 8));

        db.Products.AddRange(allProducts);
        await db.SaveChangesAsync();

        var allBuyers = buyers.ToList();
        var allAddresses = new List<Address>();
        foreach (var buyer in allBuyers)
        {
            var address1 = new Address
            {
                UserId = buyer.Id,
                Name = "Дім",
                Phone = $"+38050{new Random().Next(1000000, 9999999)}",
                Region = new[] { "Київська", "Львівська", "Одеська", "Харківська", "Дніпропетровська" }[new Random().Next(5)],
                City = new[] { "Київ", "Львів", "Одеса", "Харків", "Дніпро", "Запоріжжя", "Вінниця", "Полтава" }[new Random().Next(8)],
                Line1 = $"Вул. {new[] { "Шевченка", "Франка", "Соборна", "Пушкіна", "Миру" }[new Random().Next(5)]} {new Random().Next(1, 150)}",
                PostalCode = $"{new Random().Next(10000, 99999)}",
                IsDefault = true,
                CreatedAtUtc = DateTime.UtcNow
            };
            var address2 = new Address
            {
                UserId = buyer.Id,
                Name = "Робота",
                Phone = $"+38067{new Random().Next(1000000, 9999999)}",
                Region = address1.Region,
                City = address1.City,
                Line1 = $"Вул. {new[] { "Індустріальна", "Промислова", "Зелена", "Садова", "Центральна" }[new Random().Next(5)]} {new Random().Next(1, 80)}",
                PostalCode = address1.PostalCode,
                IsDefault = false,
                CreatedAtUtc = DateTime.UtcNow
            };
            allAddresses.AddRange(new[] { address1, address2 });
        }
        db.Addresses.AddRange(allAddresses);
        await db.SaveChangesAsync();

        var shippingMethods = new[]
        {
            new ShippingMethod { Name = ShippingMethodType.NovaPoshta, Price = 90m, EstimatedDays = 2 },
            new ShippingMethod { Name = ShippingMethodType.Courier, Price = 150m, EstimatedDays = 1 },
            new ShippingMethod { Name = ShippingMethodType.SelfPickup, Price = 0m, EstimatedDays = 0 }
        };
        db.ShippingMethods.AddRange(shippingMethods);
        await db.SaveChangesAsync();

        var reviews = new List<Review>();
        var random = new Random(12345);
        var shuffledProducts = allProducts.OrderBy(_ => random.Next()).Take(40).ToList();
        var usedPairs = new HashSet<(Guid ProductId, Guid BuyerUserId)>();

        var reviewTexts = new[]
        {
            "Чудовий товар! Рекомендую всім.",
            "Дуже задоволений покупкою, швидка доставка.",
            "Якість на висоті, розмір підійшов ідеально.",
            "Все сподобалося, буду замовляти ще.",
            "Нормальний товар за свою ціну.",
            "Відмінна якість! Купував друге.",
            "Дякую за оперативну доставку!",
            "Товар відповідає опису, рекомендую.",
            "Все чудово, дякую магазину!",
            "Швидко, якісно, недорого. Рекомендую!",
            "Товар хороший, але трохи довго йшов.",
            "Чудовий вибір, задоволений на 100%.",
            "Перший раз купую - все сподобалося!",
            "Якість відмінна, розмір підійшов.",
            "Дуже стильний товар, подобається."
        };

        foreach (var product in shuffledProducts)
        {
            var reviewer = allBuyers[random.Next(allBuyers.Count)];
            var pair = (product.Id, reviewer.Id);
            if (!usedPairs.Contains(pair))
            {
                usedPairs.Add(pair);
                reviews.Add(new Review
                {
                    ProductId = product.Id,
                    BuyerUserId = reviewer.Id,
                    Rating = random.Next(3, 6),
                    Text = reviewTexts[random.Next(reviewTexts.Length)],
                    CreatedAtUtc = DateTime.UtcNow.AddDays(-random.Next(1, 90))
                });
            }
            
            if (random.Next(3) == 0)
            {
                var reviewer2 = allBuyers[random.Next(allBuyers.Count)];
                var pair2 = (product.Id, reviewer2.Id);
                if (reviewer2.Id != reviewer.Id && !usedPairs.Contains(pair2))
                {
                    usedPairs.Add(pair2);
                    reviews.Add(new Review
                    {
                        ProductId = product.Id,
                        BuyerUserId = reviewer2.Id,
                        Rating = random.Next(3, 6),
                        Text = reviewTexts[random.Next(reviewTexts.Length)],
                        CreatedAtUtc = DateTime.UtcNow.AddDays(-random.Next(1, 90))
                    });
                }
            }
        }
        db.Reviews.AddRange(reviews);
        await db.SaveChangesAsync();

        var orderStatuses = new[] { OrderStatus.Pending, OrderStatus.Confirmed, OrderStatus.Shipped, OrderStatus.Shipped, OrderStatus.Shipped, OrderStatus.Completed, OrderStatus.Completed, OrderStatus.Completed, OrderStatus.Cancelled };
        var orders = new List<Order>();
        var orderItems = new List<OrderItem>();
        var payments = new List<Payment>();

        for (int i = 0; i < 25; i++)
        {
            var buyer = allBuyers[random.Next(allBuyers.Count)];
            var buyerAddresses = allAddresses.Where(a => a.UserId == buyer.Id).ToList();
            var address = buyerAddresses[random.Next(buyerAddresses.Count)];
            var status = orderStatuses[i % orderStatuses.Length];
            var shippingMethodId = shippingMethods[random.Next(shippingMethods.Length)].Id;
            var orderItemsCount = random.Next(1, 4);
            var orderProducts = allProducts.OrderBy(_ => random.Next()).Take(orderItemsCount).ToList();

            var order = new Order
            {
                BuyerUserId = buyer.Id,
                Status = status,
                ShippingAddressId = address.Id,
                ShippingMethodId = shippingMethodId,
                BuyerName = buyer.DisplayName,
                Phone = address.Phone,
                City = address.City,
                DeliveryAddress = address.Line1,
                Comment = random.Next(5) == 0 ? "Прошу передзвонити перед доставкою" : null,
                TrackingNumber = (status == OrderStatus.Shipped || status == OrderStatus.Completed) ? $"NP{random.Next(1000000, 9999999)}" : null,
                Total = 0,
                CreatedAtUtc = DateTime.UtcNow.AddDays(-random.Next(1, 30))
            };
            db.Orders.Add(order);
            await db.SaveChangesAsync();

            decimal total = 0;
            foreach (var product in orderProducts)
            {
                var qty = random.Next(1, 3);
                var unitPrice = product.Price;
                var lineTotal = unitPrice * qty;
                total += lineTotal;

                db.OrderItems.Add(new OrderItem
                {
                    OrderId = order.Id,
                    ProductId = product.Id,
                    StoreId = product.StoreId,
                    ProductTitleSnapshot = product.Title,
                    UnitPrice = unitPrice,
                    Quantity = qty,
                    LineTotal = lineTotal
                });
            }
            await db.SaveChangesAsync();

            order.Total = total;

            payments.Add(new Payment
            {
                OrderId = order.Id,
                Amount = total,
                PaymentMethod = PaymentMethod.LiqPay,
                Status = status == OrderStatus.Cancelled ? PaymentStatus.Failed : PaymentStatus.Completed,
                ExternalReference = $"liqpay_{Guid.NewGuid():N}",
                CreatedAtUtc = order.CreatedAtUtc
            });
        }

        db.OrderItems.AddRange(orderItems);
        await db.SaveChangesAsync();

        db.Payments.AddRange(payments);
        await db.SaveChangesAsync();

        var wishlists = new List<Wishlist>();
        foreach (var buyer in allBuyers.Take(8))
        {
            var wishlistProducts = allProducts.OrderBy(_ => random.Next()).Take(random.Next(2, 6)).ToList();
            var wishlist = new Wishlist
            {
                UserId = buyer.Id,
                Name = "Обране",
                CreatedAtUtc = DateTime.UtcNow
            };
            db.Wishlists.Add(wishlist);
            await db.SaveChangesAsync();

            foreach (var product in wishlistProducts)
            {
                db.WishlistItems.Add(new WishlistItem
                {
                    WishlistId = wishlist.Id,
                    ProductId = product.Id,
                    CreatedAtUtc = DateTime.UtcNow
                });
            }
            await db.SaveChangesAsync();
        }

        var chat = new Chat
        {
            SellerId = sellers[0].Id,
            StoreId = sportStore.Id,
            BuyerId = allBuyers[0].Id,
            CreatedAtUtc = DateTime.UtcNow.AddDays(-5)
        };
        db.Chats.Add(chat);
        await db.SaveChangesAsync();

        db.Messages.AddRange(
            new Message { ChatId = chat.Id, SenderId = allBuyers[0].Id, MessageText = "Доброго дня! Підкажіть, чи є Nike Air Force 1 розмір 42?", CreatedAtUtc = DateTime.UtcNow.AddDays(-5).AddHours(1) },
            new Message { ChatId = chat.Id, SenderId = sellers[0].Id, MessageText = "Доброго дня! Так, є в наявності. Який колір бажаєте?", CreatedAtUtc = DateTime.UtcNow.AddDays(-5).AddHours(2) },
            new Message { ChatId = chat.Id, SenderId = allBuyers[0].Id, MessageText = "Білий, будь ласка. Як швидко можна відправити?", CreatedAtUtc = DateTime.UtcNow.AddDays(-5).AddHours(3) }
        );
        await db.SaveChangesAsync();

        foreach (var buyer in allBuyers.Take(6))
        {
            db.Notifications.Add(new Notification
            {
                UserId = buyer.Id,
                Title = "Вітаємо на Promka!",
                Text = "Дякуємо за реєстрацію. Тепер ви можете купувати та продавати товари на нашому маркетплейсі.",
                IsRead = false,
                CreatedAtUtc = DateTime.UtcNow.AddDays(-random.Next(1, 7))
            });
        }
        await db.SaveChangesAsync();
    }
}
