# Структура бази даних маркетплейсу

## ASP.NET Core Identity таблиці (стандартні)

### Users (AspNetUsers)

-   Id (string) - первинний ключ
-   UserName (string) - унікальне ім'я користувача
-   Email (string) - email
-   EmailConfirmed (bool) - підтвердження email
-   PasswordHash (string) - хеш пароля
-   SecurityStamp (string) - безпечна мітка
-   PhoneNumber (string) - номер телефону
-   PhoneNumberConfirmed (bool) - підтвердження телефону
-   TwoFactorEnabled (bool) - двофакторна автентифікація
-   LockoutEndDateUtc (datetime) - дата закінчення блокування
-   LockoutEnabled (bool) - чи дозволено блокування
-   AccessFailedCount (int) - кількість невдалих спроб доступу
-   CreatedAt (datetime) - дата створення
-   UpdatedAt (datetime) - дата оновлення

### Roles (AspNetRoles)

-   Id (string) - первинний ключ
-   Name (string) - назва ролі
-   NormalizedName (string) - нормалізована назва ролі
-   ConcurrencyStamp (string) - мітка паралельності

### UserRoles (AspNetUserRoles)

-   UserId (string) - зовнішній ключ на Users
-   RoleId (string) - зовнішній ключ на Roles

### UserClaims (AspNetUserClaims)

-   Id (int) - первинний ключ
-   UserId (string) - зовнішній ключ на Users
-   ClaimType (string) - тип claim
-   ClaimValue (string) - значення claim

### UserLogins (AspNetUserLogins)

-   LoginProvider (string) - провайдер входу
-   ProviderKey (string) - ключ провайдера
-   ProviderDisplayName (string) - відображувана назва провайдера
-   UserId (string) - зовнішній ключ на Users

### UserTokens (AspNetUserTokens)

-   UserId (string) - зовнішній ключ на Users
-   LoginProvider (string) - провайдер входу
-   Name (string) - назва токена
-   Value (string) - значення токена

### RoleClaims (AspNetRoleClaims)

-   Id (int) - первинний ключ
-   RoleId (string) - зовнішній ключ на Roles
-   ClaimType (string) - тип claim
-   ClaimValue (string) - значення claim

## Додаткові таблиці для маркетплейсу

### Users (додаткові поля)

-   FirstName (string) - ім'я
-   LastName (string) - прізвище
-   Role (enum: buyer, seller, moderator, admin) - роль користувача
-   Birthday (datetime, nullable) - дата народження
-   EmailConfirmed (bool) - email підтверджено
-   PhoneNumberConfirmed (bool) - телефон підтверджено

### Companies

-   Id (int) - первинний ключ
-   Name (string) - назва компанії
-   Slug (string) - URL-дружній ідентифікатор
-   Description (string) - опис компанії
-   Image (string, nullable) - логотип компанії
-   ContactEmail (string) - контактний email
-   ContactPhone (string) - контактний телефон
-   AddressRegion (string) - регіон
-   AddressCity (string) - місто
-   AddressStreet (string) - вулиця
-   AddressPostalCode (string) - поштовий індекс
-   IsApproved (bool) - чи схвалена компанія
-   ApprovedAt (datetime, nullable) - дата схвалення
-   ApprovedById (string, nullable) - хто схвалив (FK -> Users)
-   Meta (json) - метадані
-   CreatedAt (datetime) - дата створення
-   UpdatedAt (datetime) - дата оновлення

### CompanyUsers

-   CompanyId (int) - зовнішній ключ на Companies
-   UserId (string) - зовнішній ключ на Users
-   IsOwner (bool) - чи є власником компанії

### CompanySchedules

-   Id (int) - первинний ключ
-   CompanyId (int) - зовнішній ключ на Companies
-   Day (enum: Monday, Tuesday, Wednesday, Thursday, Friday, Saturday, Sunday) - день тижня
-   OpenTime (time) - час відкриття
-   CloseTime (time) - час закриття
-   IsClosed (bool) - чи закрито

### Categories

-   Id (int) - первинний ключ
-   Name (string) - назва категорії
-   Slug (string) - URL-дружній ідентифікатор
-   Image (string, nullable) - зображення категорії
-   ParentId (int, nullable) - батьківська категорія (сам на себе)
-   Meta (json) - метадані
-   CreatedAt (datetime) - дата створення
-   UpdatedAt (datetime) - дата оновлення

### Products

-   Id (int) - первинний ключ
-   CompanyId (int) - зовнішній ключ на Companies
-   Name (string) - назва товару
-   Slug (string) - URL-дружній ідентифікатор
-   Description (string) - опис товару (HTML)
-   Price (decimal) - ціна товару
-   Stock (int) - кількість на складі
-   CategoryId (int) - зовнішній ключ на Categories
-   Attributes (json) - динамічні атрибути
-   Status (enum: pending, approved, rejected) - статус товару
-   ApprovedAt (datetime, nullable) - дата схвалення
-   ApprovedById (string, nullable) - хто схвалив (FK -> Users)
-   CreatedAt (datetime) - дата створення
-   UpdatedAt (datetime) - дата оновлення
-   Meta (json) - метадані

### ProductImages

-   Id (int) - первинний ключ
-   ProductId (int) - зовнішній ключ на Products
-   Image (string) - шлях до зображення
-   SortOrder (int) - порядок сортування
-   CreatedAt (datetime) - дата створення

### Orders

-   Id (int) - первинний ключ
-   CustomerId (string) - зовнішній ключ на Users
-   Status (enum: pending, paid, shipped, delivered, cancelled) - статус замовлення
-   TotalPrice (decimal) - загальна сума
-   ShippingAddressId (int, nullable) - зовнішній ключ на Addresses
-   ShippingMethodId (int) - зовнішній ключ на ShippingMethods
-   CreatedAt (datetime) - дата створення
-   UpdatedAt (datetime) - дата оновлення

### OrderItems

-   Id (int) - первинний ключ
-   OrderId (int) - зовнішній ключ на Orders
-   ProductId (int) - зовнішній ключ на Products
-   Quantity (int) - кількість
-   Price (decimal) - ціна за одиницю
-   CreatedAt (datetime) - дата створення
-   UpdatedAt (datetime) - дата оновлення

### ShippingMethods

-   Id (int) - первинний ключ
-   Name (enum: nova_poshta, courier, self_pickup) - назва методу доставки
-   Price (decimal) - вартість доставки
-   EstimatedDays (int) - орієнтовний термін доставки в днях
-   CreatedAt (datetime) - дата створення
-   UpdatedAt (datetime) - дата оновлення

### Payments

-   Id (int) - первинний ключ
-   OrderId (int) - зовнішній ключ на Orders
-   PaymentMethod (enum: card, paypal, bank_transfer) - метод оплати
-   Amount (decimal) - сума платежу
-   Status (enum: pending, completed, failed) - статус платежу
-   CreatedAt (datetime) - дата створення
-   UpdatedAt (datetime) - дата оновлення

### Reviews

-   Id (int) - первинний ключ
-   UserId (string) - зовнішній ключ на Users
-   ProductId (int) - зовнішній ключ на Products
-   ParentId (int, nullable) - батьківський відгук (для відповідей)
-   Rating (int, 1-5) - рейтинг
-   Comment (string) - коментар
-   CreatedAt (datetime) - дата створення
-   UpdatedAt (datetime) - дата оновлення

### Addresses

-   Id (int) - первинний ключ
-   UserId (string) - зовнішній ключ на Users
-   Street (string) - вулиця
-   City (string) - місто
-   State (string) - область/штат
-   PostalCode (string) - поштовий індекс
-   CreatedAt (datetime) - дата створення
-   UpdatedAt (datetime) - дата оновлення

### Favorites

-   Id (int) - первинний ключ
-   UserId (string) - зовнішній ключ на Users
-   ProductId (int) - зовнішній ключ на Products
-   CreatedAt (datetime) - дата створення

### Coupons

-   Id (int) - первинний ключ
-   Code (string) - код купону
-   Discount (decimal) - знижка
-   DiscountType (enum: percentage, fixed) - тип знижки
-   UsageLimit (int, nullable) - обмеження використання
-   ExpiresAt (datetime, nullable) - дата закінчення дії
-   CreatedAt (datetime) - дата створення
-   UpdatedAt (datetime) - дата оновлення

### OrderCoupons

-   Id (int) - первинний ключ
-   OrderId (int) - зовнішній ключ на Orders
-   CouponId (int) - зовнішній ключ на Coupons
-   CreatedAt (datetime) - дата створення

### Ratings

-   Id (int) - первинний ключ
-   UserId (string) - зовнішній ключ на Users
-   ProductId (int) - зовнішній ключ на Products
-   Service (int, 1-5) - оцінка обслуговування
-   DeliveryTime (int, 1-5) - оцінка часу доставки
-   Accuracy (int, 1-5) - оцінка точності
-   Comment (string, nullable) - коментар
-   CreatedAt (datetime) - дата створення
-   UpdatedAt (datetime) - дата оновлення

### Wishlists

-   Id (int) - первинний ключ
-   UserId (string) - зовнішній ключ на Users
-   Name (string) - назва вішліста
-   CreatedAt (datetime) - дата створення
-   UpdatedAt (datetime) - дата оновлення

### WishlistItems

-   Id (int) - первинний ключ
-   WishlistId (int) - зовнішній ключ на Wishlists
-   ProductId (int) - зовнішній ключ на Products
-   CreatedAt (datetime) - дата створення

### Chats

-   Id (int) - первинний ключ
-   SellerId (string) - зовнішній ключ на Users (продавець)
-   BuyerId (string) - зовнішній ключ на Users (покупець)
-   CompanyId (int, nullable) - зовнішній ключ на Companies (якщо чат з компанією)
-   CreatedAt (datetime) - дата створення

### Messages

-   Id (int) - первинний ключ
-   ChatId (int) - зовнішній ключ на Chats
-   SenderId (string) - зовнішній ключ на Users
-   Message (string) - текст повідомлення
-   IsRead (bool) - чи прочитано
-   CreatedAt (datetime) - дата створення

### Notifications

-   Id (int) - первинний ключ
-   UserId (string) - зовнішній ключ на Users
-   Title (string) - заголовок
-   Message (string) - текст повідомлення
-   IsRead (bool) - чи прочитано
-   CreatedAt (datetime) - дата створення

## Зв'язки між таблицями

1. **Users** має багато **Companies** (через CompanyUsers)
2. **Users** має багато **Orders**
3. **Users** має багато **Reviews**
4. **Users** має багато **Addresses**
5. **Users** має багато **Favorites**
6. **Users** має багато **Wishlists**
7. **Users** має багато **Chats** (як продавець або покупець)
8. **Users** має багато **Messages**
9. **Users** має багато **Notifications**

10. **Companies** має багато **Products**
11. **Companies** має багато **CompanySchedules**
12. **Companies** має багато **Users** (через CompanyUsers)

13. **Categories** має багато **Categories** (рекурсивна зв'язка)
14. **Categories** має багато **Products**

15. **Products** має багато **ProductImages**
16. **Products** має багато **Reviews**
17. **Products** має багато **OrderItems**
18. **Products** має багато **Favorites**
19. **Products** має багато **WishlistItems**

20. **Orders** має багато **OrderItems**
21. **Orders** має багато **OrderCoupons**
22. **Orders** має один **Payments**
23. **Orders** має один **ShippingMethods**
24. **Orders** має один **Addresses** (для доставки)

25. **Chats** має багато **Messages**

## Особливості структури

1. **ASP.NET Core Identity** - використовується для аутентифікації та авторизації
2. **JSON поля** - для гнучкого зберігання метаданих та динамічних атрибутів
3. **Enum поля** - для обмеження значень певних полів
4. **Soft delete** - можна додати поле IsDeleted для логічного видалення
5. **Audit поля** - CreatedAt, UpdatedAt для відстеження змін
6. **Soft relationships** - деякі зв'язки можуть бути nullable для гнучкості
