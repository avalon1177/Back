1. ASP.NET Minimal API (без контролерів)
2. PostgreSQL
3. EF Core (тут ми пишем окремі конфігураційні класи для сутностей)
4. Humanizer
5. ASP.NET Identity (максимально, все що відноситься аутентифікації та авторизації та акаунтінгу)
6. Docker Compose (треба подружитись)
7. Статичний аналізатор і форматувальник коду на C# (Sonar Analizer або https://csharpier.com/)
8. FluentValidation
9. AutoMapper або Mapster
10. MediatR
11. Hangfire або Quartz.NET
12. MinIO - файли будем зберігати в AWS s3 compatible інструменти (короче файли будуть на іншому сервері, так правильно)
13. Bogus - фейкер
14. Serilog

# Сутності системи Marketplace

## ASP.NET Core Identity сутності (стандартні)

### User (AspNetUsers)

-   **Опис**: Основна сутність для реєстрації та авторизації користувачів
-   **Поля**:
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

### Role (AspNetRoles)

-   **Опис**: Ролі користувачів
-   **Поля**:
    -   Id (string) - первинний ключ
    -   Name (string) - назва ролі
    -   NormalizedName (string) - нормалізована назва ролі
    -   ConcurrencyStamp (string) - мітка паралельності

### UserRole (AspNetUserRoles)

-   **Опис**: Зв'язок між користувачами та ролями
-   **Поля**:
    -   UserId (string) - зовнішній ключ на Users
    -   RoleId (string) - зовнішній ключ на Roles

### UserClaim (AspNetUserClaims)

-   **Опис**: Claims користувачів
-   **Поля**:
    -   Id (int) - первинний ключ
    -   UserId (string) - зовнішній ключ на Users
    -   ClaimType (string) - тип claim
    -   ClaimValue (string) - значення claim

### UserLogin (AspNetUserLogins)

-   **Опис**: Інформація про входи користувачів
-   **Поля**:
    -   LoginProvider (string) - провайдер входу
    -   ProviderKey (string) - ключ провайдера
    -   ProviderDisplayName (string) - відображувана назва провайдера
    -   UserId (string) - зовнішній ключ на Users

### UserToken (AspNetUserTokens)

-   **Опис**: Токени користувачів
-   **Поля**:
    -   UserId (string) - зовнішній ключ на Users
    -   LoginProvider (string) - провайдер входу
    -   Name (string) - назва токена
    -   Value (string) - значення токена

### RoleClaim (AspNetRoleClaims)

-   **Опис**: Claims ролей
-   **Поля**:
    -   Id (int) - первинний ключ
    -   RoleId (string) - зовнішній ключ на Roles
    -   ClaimType (string) - тип claim
    -   ClaimValue (string) - значення claim

## Користувачі та авторизація

### User (Користувач)

-   **Опис**: Основна сутність для реєстрації та авторизації користувачів
-   **Поля**:
    -   Id (Guid) - унікальний ідентифікатор
    -   Username (string) - ім'я користувача
    -   Email (Email) - електронна пошта
    -   Password (Password) - хеш пароля
    -   Birthday (DateTime?) - дата народження
    -   Role (Role) - роль користувача
    -   LastSeenAt (DateTime?) - час останнього візиту
    -   EmailConfirmed (bool) - підтвердження email
    -   IsApproved (bool) - схвалення адміністратором
    -   ApprovedByUserId (Guid?) - хто схвалив
-   **Ролі**: Buyer, Seller, SellerOwner, Moderator, Admin

### Address (Адреса)

-   **Опис**: Адреса користувача або для доставки
-   **Поля**:
    -   Id (Guid)
    -   UserId (Guid?) - власник адреси
    -   AddressVO - об'єкт адреси (місто, вулиця, регіон, поштовий індекс)

## Компанії та продавці

### Company (Компанія)

-   **Опис**: Компанія-продавець на маркетплейсі
-   **Поля**:
    -   Id (Guid)
    -   Name (string) - назва компанії
    -   Slug (Slug) - URL-ідентифікатор
    -   Description (string) - опис
    -   Image (Url) - логотип
    -   ContactEmail (Email) - контактна пошта
    -   ContactPhone (Phone) - контактний телефон
    -   Address (AddressVO) - адреса компанії
    -   IsFeatured (bool) - чи є рекомендованою
    -   IsApproved (bool) - схвалена модератором
    -   ApprovedByUserId (Guid?) - хто схвалив
    -   Meta (Meta) - метадані для SEO

### CompanyFinance (Фінансові дані компанії)

-   **Опис**: Банківські реквізити для отримання платежів
-   **Поля**:
    -   Id (Guid)
    -   CompanyId (Guid) - власник
    -   BankAccount (string) - рахунок
    -   BankName (string) - назва банку
    -   BankCode (string) - код банку
    -   TaxId (string) - ІПН
    -   PaymentDetails (string) - додаткові реквізити

### CompanySchedule (Графік роботи компанії)

-   **Опис**: Розклад роботи компанії по днях тижня
-   **Поля**:
    -   Id (Guid)
    -   CompanyId (Guid)
    -   OpenTime (TimeSpan) - час відкриття
    -   CloseTime (TimeSpan) - час закриття
    -   IsClosed (bool) - чи закрито
    -   Day (Day) - день тижня

### CompanyUser (Працівник компанії)

-   **Опис**: Зв'язок між користувачем та компанією
-   **Поля**:
    -   Id (Guid)
    -   CompanyId (Guid)
    -   UserId (Guid)
    -   Role (string) - роль у компанії
    -   IsOwner (bool) - чи є власником компанії

### SellerRequest (Заявка на продавця)

-   **Опис**: Заявка користувача на отримання прав продавця
-   **Поля**:
    -   Id (Guid)
    -   UserId (Guid) - хто подав заявку
    -   CompanyId (Guid) - компанія
    -   AdditionalInformation (string) - додаткова інформація
    -   Status (RequestStatus) - статус: Pending, Approved, Rejected
    -   ApprovedAt (DateTime?) - коли схвалено
    -   ApprovedByUser (Guid?) - хто схвалив
    -   RejectedAt (DateTime?) - коли відхилено
    -   RejectedByUser (Guid?) - хто відхилив

## Товари та категорії

### Category (Категорія)

-   **Опис**: Категорія товарів (деревоподібна структура)
-   **Поля**:
    -   Id (Guid)
    -   Name (string) - назва
    -   Slug (Slug) - URL-ідентифікатор
    -   Description (string) - опис
    -   Image (Url?) - зображення
    -   ParentId (Guid?) - батьківська категорія
    -   Meta (Meta) - метадані

### Product (Товар)

-   **Опис**: Товар, який продається на маркетплейсі
-   **Поля**:
    -   Id (Guid)
    -   CompanyId (Guid) - власник
    -   Name (string) - назва
    -   Slug (Slug) - URL-ідентифікатор
    -   Description (string) - опис
    -   Price (Money) - ціна
    -   Stock (uint) - кількість на складі
    -   CategoryId (Guid) - категорія
    -   Attributes (Dictionary<string, string>) - додаткові атрибути (це або JSON або ARRAY (KEY-VALUE))
    -   Status (ProductStatus) - статус: Pending, Approved, Rejected
    -   IsApproved (bool) - схвалено модератором
    -   ApprovedAt (DateTime?) - коли схвалено
    -   ApprovedByUserId (Guid?) - хто схвалив
    -   Meta (Meta) - метадані

### ProductImage (Зображення товару)

-   **Опис**: Зображення для товару
-   **Поля**:
    -   Id (Guid)
    -   ProductId (Guid)
    -   Image (Url) - посилання на зображення
    -   Order (int) - порядок відображення
    -   SortOrder (int) - порядок сортування

## Кошик та замовлення

### Cart (Кошик)

-   **Опис**: Кошик користувача
-   **Поля**:
    -   Id (Guid)
    -   UserId (Guid) - власник

### CartItem (Елемент кошика)

-   **Опис**: Товар у кошику
-   **Поля**:
    -   Id (Guid)
    -   CartId (Guid)
    -   ProductId (Guid)
    -   Quantity (uint) - кількість

### Order (Замовлення)

-   **Опис**: Замовлення покупця
-   **Поля**:
    -   Id (Guid)
    -   CustomerId (Guid) - покупець
    -   TotalPrice (Money) - загальна сума
    -   Status (OrderStatus) - статус: Pending, Paid, Shipped, Delivered, Cancelled
    -   ShippingAddressId (Guid) - адреса доставки
    -   ShippingMethodId (Guid) - спосіб доставки

### OrderItem (Позиція замовлення)

-   **Опис**: Конкретний товар у замовленні
-   **Поля**:
    -   Id (Guid)
    -   OrderId (Guid)
    -   ProductId (Guid)
    -   Quantity (uint)
    -   Price (Money) - ціна на момент замовлення

### OrderCoupon (Купон до замовлення)

-   **Опис**: Застосований купон до замовлення
-   **Поля**:
    -   Id (Guid)
    -   OrderId (Guid)
    -   CouponId (Guid)

### Payment (Оплата)

-   **Опис**: Платіж за замовлення
-   **Поля**:
    -   Id (Guid)
    -   OrderId (Guid)
    -   PaymentMethod (PaymentMethod) - спосіб оплати: Card, PayPal, BankTransfer
    -   Amount (Money) - сума
    -   Status (PaymentStatus) - статус: Pending, Completed, Failed

### ShippingMethod (Спосіб доставки)

-   **Опис**: Спосіб доставки
-   **Поля**:
    -   Id (Guid)
    -   Price (Money) - вартість доставки
    -   EstimatedDays (uint) - орієнтовний термін
    -   Name (Name) - назва: NovaPoshta, Courier, SelfPickup

## Промоакції

### Coupon (Купон)

-   **Опис**: Знижковий купон
-   **Поля**:
    -   Id (Guid)
    -   Code (string) - код купона
    -   Discount (double) - розмір знижки
    -   DiscountType (DiscountType) - тип: Percentage, Fixed
    -   UsageLimit (uint?) - ліміт використань
    -   ExpiresAt (DateTime?) - дата закінчення

## Спілкування

### Chat (Чат)

-   **Опис**: Чат між покупцем та продавцем
-   **Поля**:
    -   Id (Guid)
    -   SellerId (Guid) - продавець
    -   BuyerId (Guid) - покупець
    -   CompanyId (Guid?) - компанія (якщо чат з компанією)

### Message (Повідомлення)

-   **Опис**: Повідомлення у чаті
-   **Поля**:
    -   Id (Guid)
    -   ChatId (Guid)
    -   SenderId (Guid)
    -   MessageText (string) - текст повідомлення
    -   IsRead (bool) - прочитано

## Відгуки та рейтинги

### Rating (Рейтинг)

-   **Опис**: Оцінка товару за різними критеріями
-   **Поля**:
    -   Id (Guid)
    -   UserId (Guid)
    -   ProductId (Guid)
    -   Service (int) - оцінка сервісу (1-5)
    -   DeliveryTime (int) - оцінка швидкості доставки (1-5)
    -   Accuracy (int) - оцінка відповідності опису (1-5)
    -   Comment (string?) - коментар

### Review (Відгук)

-   **Опис**: Відгук про товар
-   **Поля**:
    -   Id (Guid)
    -   UserId (Guid)
    -   ProductId (Guid)
    -   RatingId (Guid)
    -   ParentId (Guid?) - відповідь на відгук
    -   Comment (string) - текст відгуку

## Обране

### Wishlist (Список бажань)

-   **Опис**: Список бажань користувача
-   **Поля**:
    -   Id (Guid)
    -   UserId (Guid)
    -   Name (string) - назва списку

### WishlistItem (Елемент списку бажань)

-   **Опис**: Товар у списку бажань
-   **Поля**:
    -   Id (Guid)
    -   WishlistId (Guid)
    -   ProductId (Guid)

### Favorite (Улюблений товар)

-   **Опис**: Улюблений товар користувача
-   **Поля**:
    -   Id (Guid)
    -   UserId (Guid)
    -   ProductId (Guid)

## Сповіщення

### Notification (Сповіщення)

-   **Опис**: Системне сповіщення користувача
-   **Поля**:
    -   Id (Guid)
    -   UserId (Guid)
    -   Title (string) - заголовок
    -   Text (string) - текст
    -   IsRead (bool) - прочитано

## Значення (Value Objects)

### Money (Гроші)

-   **Опис**: Валютне значення
-   **Поля**:
    -   Amount (decimal) - сума
    -   Currency (Currency) - валюта

### Email (Електронна пошта)

-   **Опис**: Електронна адреса
-   **Поля**:
    -   Value (string) - значення email

### Password (Пароль)

-   **Опис**: Хеш пароля
-   **Поля**:
    -   Value (string) - хешоване значення

### Phone (Телефон)

-   **Опис**: Номер телефону
-   **Поля**:
    -   Value (string) - номер

### Url (URL)

-   **Опис**: Веб-адреса
-   **Поля**:
    -   Value (string) - URL

### Slug (URL-ідентифікатор)

-   **Опис**: URL-дружній ідентифікатор
-   **Поля**:
    -   Value (string) - значення

### AddressVO (Адреса)

-   **Опис**: Об'єкт адреси
-   **Поля**:
    -   Region (string) - регіон
    -   City (string) - місто
    -   Street (string) - вулиця
    -   PostalCode (string) - поштовий індекс

### Meta (Метадані)

-   **Опис**: Метадані для SEO
-   **Поля**:
    -   Title (string) - заголовок
    -   Description (string) - опис
    -   Image (Url) - зображення

## Зв'язки між сутностями

### Основні зв'язки:

1. **User → Company**: Користувач може мати багато компаній (1:N)
2. **User → Address**: Користувач може мати багато адрес (1:N)
3. **User → Cart**: Користувач має один кошик (1:1)
4. **User → Order**: Користувач може мати багато замовлень (1:N)
5. **User → Review**: Користувач може писати багато відгуків (1:N)
6. **User → Rating**: Користувач може ставити багато оцінок (1:N)
7. **User → Wishlist**: Користувач може мати багато списків бажань (1:N)
8. **User → Favorite**: Користувач може мати багато улюблених товарів (1:N)
9. **User → Notification**: Користувач може отримувати багато сповіщень (1:N)
10. **User → Chat**: Користувач може мати багато чатів (1:N)

11. **Company → Product**: Компанія може мати багато товарів (1:N)
12. **Company → CompanyFinance**: Компанія має один фінансовий запис (1:1)
13. **Company → CompanySchedule**: Компанія має розклад по днях (1:N)
14. **Company → CompanyUser**: Компанія може мати багато працівників (1:N)
15. **Company → SellerRequest**: Компанія може мати багато заявок (1:N)

16. **Category → Category**: Категорії утворюють дерево (N:1)
17. **Category → Product**: Категорія може мати багато товарів (1:N)

18. **Product → ProductImage**: Товар може мати багато зображень (1:N)
19. **Product → Review**: Товар може мати багато відгуків (1:N)
20. **Product → Rating**: Товар може мати багато оцінок (1:N)
21. **Product → WishlistItem**: Товар може бути у багатьох списках бажань (1:N)
22. **Product → Favorite**: Товар може бути улюбленим у багатьох користувачів (1:N)

23. **Cart → CartItem**: Кошик може мати багато товарів (1:N)
24. **Order → OrderItem**: Замовлення може мати багато позицій (1:N)
25. **Order → OrderCoupon**: Замовлення може мати багато купонів (1:N)
26. **Order → Payment**: Замовлення може мати багато платежів (1:N)
27. **Chat → Message**: Чат може мати багато повідомлень (1:N)
28. **Wishlist → WishlistItem**: Список бажань може мати багато товарів (1:N)

### Складніші зв'язки:

-   **Review → Review**: Відгук може мати відповіді (рекурсивний N:1)
-   **Message → User**: Повідомлення має відправника (N:1)
-   **Order → Address**: Замовлення має адресу доставки (N:1)
-   **Order → ShippingMethod**: Замовлення має спосіб доставки (N:1)
-   **OrderItem → Product**: Позиція має конкретний товар (N:1)
-   **OrderCoupon → Coupon**: Застосований купон (N:1)
-   **Payment → Order**: Платіж належить замовленню (N:1)
-   **CompanyUser → User**: Працівник - це користувач (N:1)
-   **CompanyUser → Company**: Працівник працює у компанії (N:1)

## Особливості структури

1. **ASP.NET Core Identity** - використовується для аутентифікації та авторизації
2. **JSON поля** - для гнучкого зберігання метаданих та динамічних атрибутів
3. **Enum поля** - для обмеження значень певних полів
4. **Soft delete** - можна додати поле IsDeleted для логічного видалення
5. **Audit поля** - CreatedAt, UpdatedAt для відстеження змін
6. **Soft relationships** - деякі зв'язки можуть бути nullable для гнучкості
