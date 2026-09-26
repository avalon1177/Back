# One File Architecture: Приклади коду для маркетплейсу

## Структура папок

```
src/
├── Marketplace.Api/
│   └── Controllers/
│       ├── ProductsController.cs
│       ├── OrdersController.cs
│       └── UsersController.cs
├── Marketplace.Application/
│   └── Features/
│       ├── Products/
│       │   ├── CreateProduct/
│       │   │   └── CreateProductCommand.cs
│       │   ├── GetProduct/
│       │   │   └── GetProductQuery.cs
│       │   ├── UpdateProduct/
│       │   │   └── UpdateProductCommand.cs
│       │   └── DeleteProduct/
│       │       └── DeleteProductCommand.cs
│       ├── Orders/
│       │   ├── CreateOrder/
│       │   │   └── CreateOrderCommand.cs
│       │   ├── GetOrder/
│       │   │   └── GetOrderQuery.cs
│       │   └── CancelOrder/
│       │       └── CancelOrderCommand.cs
│       └── Users/
│           ├── RegisterUser/
│           │   └── RegisterUserCommand.cs
│           └── LoginUser/
│               └── LoginUserQuery.cs
├── Marketplace.Domain/
│   ├── Entities/
│   │   ├── User.cs
│   │   ├── Product.cs
│   │   └── Order.cs
│   └── ValueObjects/
│       ├── Money.cs
│       └── Email.cs
└── Marketplace.Infrastructure/
    ├── Repositories/
    │   ├── ProductRepository.cs
    │   └── OrderRepository.cs
    └── Persistence/
        └── ApplicationDbContext.cs
```

## Приклад 1: CreateProductCommand.cs (One File)

```csharp
using System;
using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Marketplace.Domain.Entities;
using Marketplace.Domain.ValueObjects;
using Marketplace.Infrastructure.Repositories;

namespace Marketplace.Application.Features.Products.CreateProduct
{
    // 1. Command (Команда)
    public class CreateProductCommand : IRequest<CreateProductResponse>
    {
        [Required]
        public string Name { get; set; }

        [Required]
        public string Description { get; set; }

        [Required]
        public decimal Price { get; set; }

        [Required]
        public int Stock { get; set; }

        [Required]
        public Guid CategoryId { get; set; }

        [Required]
        public Guid CompanyId { get; set; }

        public string ImageUrl { get; set; }
        public string Slug { get; set; }
    }

    // 2. Response (Відповідь)
    public class CreateProductResponse
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string Slug { get; set; }
        public string Description { get; set; }
        public decimal Price { get; set; }
        public int Stock { get; set; }
        public Guid CategoryId { get; set; }
        public Guid CompanyId { get; set; }
        public string ImageUrl { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    // 3. Validator (Валідатор)
    public class CreateProductValidator : AbstractValidator<CreateProductCommand>
    {
        public CreateProductValidator()
        {
            RuleFor(x => x.Name)
                .NotEmpty()
                .MinimumLength(2)
                .MaximumLength(200);

            RuleFor(x => x.Description)
                .NotEmpty()
                .MinimumLength(10);

            RuleFor(x => x.Price)
                .GreaterThan(0)
                .LessThan(1000000);

            RuleFor(x => x.Stock)
                .GreaterThanOrEqualTo(0);

            RuleFor(x => x.CategoryId)
                .NotEmpty();

            RuleFor(x => x.CompanyId)
                .NotEmpty();
        }
    }

    // 4. Handler (Обробник)
    public class CreateProductHandler : IRequestHandler<CreateProductCommand, CreateProductResponse>
    {
        private readonly IProductRepository _productRepository;
        private readonly ICategoryRepository _categoryRepository;
        private readonly ICompanyRepository _companyRepository;

        public CreateProductHandler(
            IProductRepository productRepository,
            ICategoryRepository categoryRepository,
            ICompanyRepository companyRepository)
        {
            _productRepository = productRepository;
            _categoryRepository = categoryRepository;
            _companyRepository = companyRepository;
        }

        public async Task<CreateProductResponse> Handle(CreateProductCommand request, CancellationToken cancellationToken)
        {
            // Перевірка існування категорії
            var category = await _categoryRepository.GetByIdAsync(request.CategoryId, cancellationToken);
            if (category == null)
            {
                throw new ArgumentException("Категорія не знайдена");
            }

            // Перевірка існування компанії
            var company = await _companyRepository.GetByIdAsync(request.CompanyId, cancellationToken);
            if (company == null)
            {
                throw new ArgumentException("Компанія не знайдена");
            }

            // Створення товару
            var product = new Product
            {
                Id = Guid.NewGuid(),
                Name = request.Name,
                Description = request.Description,
                Price = new Money(request.Price, "UAH"),
                Stock = request.Stock,
                CategoryId = request.CategoryId,
                CompanyId = request.CompanyId,
                ImageUrl = request.ImageUrl,
                Slug = string.IsNullOrEmpty(request.Slug)
                    ? GenerateSlug(request.Name)
                    : request.Slug,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            // Збереження
            await _productRepository.AddAsync(product, cancellationToken);

            // Повернення відповіді
            return new CreateProductResponse
            {
                Id = product.Id,
                Name = product.Name,
                Slug = product.Slug,
                Description = product.Description,
                Price = product.Price.Amount,
                Stock = product.Stock,
                CategoryId = product.CategoryId,
                CompanyId = product.CompanyId,
                ImageUrl = product.ImageUrl,
                CreatedAt = product.CreatedAt
            };
        }

        private string GenerateSlug(string name)
        {
            return name
                .ToLowerInvariant()
                .Replace(" ", "-")
                .Replace("&", "and")
                .Replace("/", "-")
                .Replace("\\", "-")
                .Replace("?", "")
                .Replace("!", "")
                .Replace(".", "")
                .Replace(",", "")
                .Replace(";", "")
                .Replace(":", "")
                .Replace("(", "")
                .Replace(")", "")
                .Replace("[", "")
                .Replace("]", "")
                .Replace("{", "")
                .Replace("}", "")
                .Replace("'", "")
                .Replace("\"", "")
                .Replace("`", "")
                .Replace("~", "")
                .Replace("@", "")
                .Replace("#", "")
                .Replace("$", "")
                .Replace("%", "")
                .Replace("^", "")
                .Replace("*", "")
                .Replace("+", "")
                .Replace("=", "")
                .Replace("<", "")
                .Replace(">", "")
                .Replace("|", "")
                .Replace("_", "-");
        }
    }
}
```

## Приклад 2: GetProductQuery.cs (One File)

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Marketplace.Domain.Entities;
using Marketplace.Infrastructure.Repositories;

namespace Marketplace.Application.Features.Products.GetProduct
{
    // 1. Query (Запит)
    public class GetProductQuery : IRequest<GetProductResponse>
    {
        public Guid ProductId { get; set; }
    }

    // 2. Response (Відповідь)
    public class GetProductResponse
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string Slug { get; set; }
        public string Description { get; set; }
        public decimal Price { get; set; }
        public int Stock { get; set; }
        public string CategoryName { get; set; }
        public string CompanyName { get; set; }
        public string ImageUrl { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    // 3. Handler (Обробник)
    public class GetProductHandler : IRequestHandler<GetProductQuery, GetProductResponse>
    {
        private readonly IProductRepository _productRepository;

        public GetProductHandler(IProductRepository productRepository)
        {
            _productRepository = productRepository;
        }

        public async Task<GetProductResponse> Handle(GetProductQuery request, CancellationToken cancellationToken)
        {
            var product = await _productRepository.GetByIdWithDetailsAsync(request.ProductId, cancellationToken);

            if (product == null)
            {
                throw new ArgumentException("Товар не знайдено");
            }

            return new GetProductResponse
            {
                Id = product.Id,
                Name = product.Name,
                Slug = product.Slug,
                Description = product.Description,
                Price = product.Price.Amount,
                Stock = product.Stock,
                CategoryName = product.Category?.Name,
                CompanyName = product.Company?.Name,
                ImageUrl = product.ImageUrl,
                CreatedAt = product.CreatedAt,
                UpdatedAt = product.UpdatedAt
            };
        }
    }
}
```

## Приклад 3: CreateOrderCommand.cs (One File)

```csharp
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Marketplace.Domain.Entities;
using Marketplace.Domain.ValueObjects;
using Marketplace.Infrastructure.Repositories;

namespace Marketplace.Application.Features.Orders.CreateOrder
{
    // 1. Command (Команда)
    public class CreateOrderCommand : IRequest<CreateOrderResponse>
    {
        [Required]
        public Guid CustomerId { get; set; }

        [Required]
        public Guid ShippingAddressId { get; set; }

        [Required]
        public Guid ShippingMethodId { get; set; }

        [Required]
        public List<OrderItemDto> Items { get; set; }

        public string CouponCode { get; set; }
    }

    public class OrderItemDto
    {
        [Required]
        public Guid ProductId { get; set; }

        [Required]
        [Range(1, int.MaxValue)]
        public int Quantity { get; set; }
    }

    // 2. Response (Відповідь)
    public class CreateOrderResponse
    {
        public Guid Id { get; set; }
        public Guid CustomerId { get; set; }
        public decimal TotalPrice { get; set; }
        public string Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<OrderItemResponse> Items { get; set; }
    }

    public class OrderItemResponse
    {
        public Guid ProductId { get; set; }
        public string ProductName { get; set; }
        public decimal Price { get; set; }
        public int Quantity { get; set; }
        public decimal TotalPrice { get; set; }
    }

    // 3. Validator (Валідатор)
    public class CreateOrderValidator : AbstractValidator<CreateOrderCommand>
    {
        public CreateOrderValidator()
        {
            RuleFor(x => x.CustomerId)
                .NotEmpty();

            RuleFor(x => x.ShippingAddressId)
                .NotEmpty();

            RuleFor(x => x.ShippingMethodId)
                .NotEmpty();

            RuleFor(x => x.Items)
                .NotEmpty()
                .WithMessage("Замовлення має містити хоча б один товар");

            RuleForEach(x => x.Items)
                .SetValidator(new OrderItemValidator());
        }

        private class OrderItemValidator : AbstractValidator<OrderItemDto>
        {
            public OrderItemValidator()
            {
                RuleFor(x => x.ProductId)
                    .NotEmpty();

                RuleFor(x => x.Quantity)
                    .GreaterThan(0);
            }
        }
    }

    // 4. Handler (Обробник)
    public class CreateOrderHandler : IRequestHandler<CreateOrderCommand, CreateOrderResponse>
    {
        private readonly IOrderRepository _orderRepository;
        private readonly IProductRepository _productRepository;
        private readonly IUserRepository _userRepository;
        private readonly IAddressRepository _addressRepository;
        private readonly IShippingMethodRepository _shippingMethodRepository;

        public CreateOrderHandler(
            IOrderRepository orderRepository,
            IProductRepository productRepository,
            IUserRepository userRepository,
            IAddressRepository addressRepository,
            IShippingMethodRepository shippingMethodRepository)
        {
            _orderRepository = orderRepository;
            _productRepository = productRepository;
            _userRepository = userRepository;
            _addressRepository = addressRepository;
            _shippingMethodRepository = shippingMethodRepository;
        }

        public async Task<CreateOrderResponse> Handle(CreateOrderCommand request, CancellationToken cancellationToken)
        {
            // Перевірка користувача
            var customer = await _userRepository.GetByIdAsync(request.CustomerId, cancellationToken);
            if (customer == null)
            {
                throw new ArgumentException("Користувач не знайдений");
            }

            // Перевірка адреси доставки
            var address = await _addressRepository.GetByIdAsync(request.ShippingAddressId, cancellationToken);
            if (address == null)
            {
                throw new ArgumentException("Адреса доставки не знайдена");
            }

            // Перевірка способу доставки
            var shippingMethod = await _shippingMethodRepository.GetByIdAsync(request.ShippingMethodId, cancellationToken);
            if (shippingMethod == null)
            {
                throw new ArgumentException("Спосіб доставки не знайдений");
            }

            // Перевірка товарів та розрахунок ціни
            var orderItems = new List<OrderItem>();
            decimal totalPrice = 0;

            foreach (var itemDto in request.Items)
            {
                var product = await _productRepository.GetByIdAsync(itemDto.ProductId, cancellationToken);
                if (product == null)
                {
                    throw new ArgumentException($"Товар з ID {itemDto.ProductId} не знайдений");
                }

                if (product.Stock < itemDto.Quantity)
                {
                    throw new ArgumentException($"Недостатньо товару '{product.Name}' на складі");
                }

                var itemTotalPrice = product.Price.Amount * itemDto.Quantity;
                totalPrice += itemTotalPrice;

                orderItems.Add(new OrderItem
                {
                    ProductId = product.Id,
                    Quantity = itemDto.Quantity,
                    Price = product.Price.Amount
                });
            }

            // Додавання вартості доставки
            totalPrice += shippingMethod.Price.Amount;

            // Створення замовлення
            var order = new Order
            {
                Id = Guid.NewGuid(),
                CustomerId = request.CustomerId,
                TotalPrice = new Money(totalPrice, "UAH"),
                Status = OrderStatus.Pending,
                ShippingAddressId = request.ShippingAddressId,
                ShippingMethodId = request.ShippingMethodId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                Items = orderItems
            };

            // Збереження
            await _orderRepository.AddAsync(order, cancellationToken);

            // Повернення відповіді
            return new CreateOrderResponse
            {
                Id = order.Id,
                CustomerId = order.CustomerId,
                TotalPrice = order.TotalPrice.Amount,
                Status = order.Status.ToString(),
                CreatedAt = order.CreatedAt,
                Items = orderItems.Select(item => new OrderItemResponse
                {
                    ProductId = item.ProductId,
                    ProductName = item.Product?.Name,
                    Price = item.Price,
                    Quantity = item.Quantity,
                    TotalPrice = item.Price * item.Quantity
                }).ToList()
            };
        }
    }
}
```

## Приклад 4: RegisterUserCommand.cs (One File)

```csharp
using System;
using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Marketplace.Domain.Entities;
using Marketplace.Domain.ValueObjects;
using Marketplace.Infrastructure.Repositories;

namespace Marketplace.Application.Features.Users.RegisterUser
{
    // 1. Command (Команда)
    public class RegisterUserCommand : IRequest<RegisterUserResponse>
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; }

        [Required]
        [StringLength(100, MinimumLength = 6)]
        public string Password { get; set; }

        [Required]
        [StringLength(50)]
        public string FirstName { get; set; }

        [Required]
        [StringLength(50)]
        public string LastName { get; set; }

        public DateTime? Birthday { get; set; }
    }

    // 2. Response (Відповідь)
    public class RegisterUserResponse
    {
        public Guid Id { get; set; }
        public string Email { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Role { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool EmailConfirmed { get; set; }
    }

    // 3. Validator (Валідатор)
    public class RegisterUserValidator : AbstractValidator<RegisterUserCommand>
    {
        public RegisterUserValidator()
        {
            RuleFor(x => x.Email)
                .NotEmpty()
                .EmailAddress();

            RuleFor(x => x.Password)
                .NotEmpty()
                .MinimumLength(6)
                .MaximumLength(100);

            RuleFor(x => x.FirstName)
                .NotEmpty()
                .MinimumLength(2)
                .MaximumLength(50);

            RuleFor(x => x.LastName)
                .NotEmpty()
                .MinimumLength(2)
                .MaximumLength(50);

            RuleFor(x => x.Birthday)
                .LessThan(DateTime.UtcNow)
                .When(x => x.Birthday.HasValue);
        }
    }

    // 4. Handler (Обробник)
    public class RegisterUserHandler : IRequestHandler<RegisterUserCommand, RegisterUserResponse>
    {
        private readonly UserManager<User> _userManager;
        private readonly IUserRepository _userRepository;

        public RegisterUserHandler(
            UserManager<User> userManager,
            IUserRepository userRepository)
        {
            _userManager = userManager;
            _userRepository = userRepository;
        }

        public async Task<RegisterUserResponse> Handle(RegisterUserCommand request, CancellationToken cancellationToken)
        {
            // Перевірка, чи користувач вже існує
            var existingUser = await _userManager.FindByEmailAsync(request.Email);
            if (existingUser != null)
            {
                throw new ArgumentException("Користувач з такою електронною поштою вже існує");
            }

            // Створення користувача
            var user = new User
            {
                Id = Guid.NewGuid(),
                UserName = request.Email,
                Email = request.Email,
                FirstName = request.FirstName,
                LastName = request.LastName,
                Birthday = request.Birthday,
                Role = UserRole.Buyer,
                EmailConfirmed = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            // Створення облікового запису
            var result = await _userManager.CreateAsync(user, request.Password);

            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                throw new ArgumentException($"Помилка реєстрації: {errors}");
            }

            // Додавання ролі
            await _userManager.AddToRoleAsync(user, UserRole.Buyer.ToString());

            // Повернення відповіді
            return new RegisterUserResponse
            {
                Id = user.Id,
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Role = user.Role.ToString(),
                CreatedAt = user.CreatedAt,
                EmailConfirmed = user.EmailConfirmed
            };
        }
    }
}
```

## Переваги One File підходу:

1. **Все в одному місці**: Command, Handler, Validator, Response - все разом
2. **Проста навігація**: Легко знайти все, що стосується функції
3. **Швидка розробка**: Не треба створювати багато файлів
4. **Чітка відокремленість**: Кожна функція - окремий файл
5. **Просте тестування**: Легко знайти все для тестування

## Недоліки:

1. **Великі файли**: Один файл може стати дуже великим
2. **Складніше тестування**: Важче тестувати окремі частини
3. **Конфлікти при merge**: Великі файли частіше виникають у конфліктах
4. **Складніше знайти конкретну частину**: Наприклад, тільки Handler

Ця архітектура ідеально підходить для початку розробки маркетплейсу, коли важливо швидко рухатися та експериментувати з функціоналом.
