Це суттєве уточнення. Якщо фронтенд на **Next.js** використовує **NextAuth.js (Auth.js)**, то бекенд на ASP.NET має працювати як **External Provider** або через **Credentials Provider**.

Оскільки ви хочете **JWT + Refresh Tokens**, архітектура буде такою: бекенд видає пару токенів, а NextAuth зберігає їх у своєму сесійному об'єкті (на стороні сервера Next.js) і оновлює через `callbacks`.

Ось опис таски, адаптований для зручної інтеграції:

---

### Task: Authentication System (Backend for NextAuth.js)

**Технології:** ASP.NET Identity, JWT, Entity Framework Core, PostgreSQL.

#### 1. Ендпоінти (API Design)

Для повної сумісності з NextAuth реалізувати наступні маршрути:

-   **`POST /api/auth/register`** — Реєстрація (FluentValidation: email, password).
-   **`POST /api/auth/login`** — Повертає `AccessToken`, `RefreshToken` та `UserDTO`.
-   **`POST /api/auth/refresh`** — Отримання нової пари токенів за допомогою діючого `RefreshToken`.
-   **`POST /api/auth/external-login`** — Спеціальний ендпоінт для Google. NextAuth передає `id_token` або `access_token` від Google, бекенд валідує його через бібліотеку Google, створює юзера в БД (якщо немає) і видає системний JWT.
-   **`POST /api/auth/2fa/verify`** — Перевірка коду (TOTP/SMS) під час логіну.

#### 2. JWT & Security Configuration

-   **Схема:** Bearer Authentication.
-   **Payload:** Додати в JWT `sub` (UserId), `email`, `role`, та `jti`.
-   **Refresh Token:** Зберігати в базі (таблиця `UserRefreshTokens`) з прив'язкою до `UserId`, `DeviceId` (опціонально) та `ExpiryDate`.
-   **CORS:** Налаштувати політику для домену фронтенда (Next.js), дозволивши заголовки `Authorization`.

#### 3. Flow для 2FA (TOTP/SMS)

Щоб фронтендеру було зручно:

1. Якщо у юзера увімкнено 2FA, `POST /login` повертає статус `202 Accepted` і `TwoFactorToken`.
2. Фронтенд бачить статус, показує поле для коду і відправляє код разом із токеном на `/2fa/verify`.
3. Тільки після цього видається фінальний JWT.

#### 4. Email & Password Logic (Background Tasks)

-   **Email Confirmation:** Відправка лінка з токеном на пошту (використовувати `Identity.GenerateEmailConfirmationTokenAsync`).
-   **Forgot Password:** Генерація токена скидання та відправка листа.
-   **Integration with Next.js:** Лінки в листах повинні вести на фронтенд: `https://marketplace.com/reset-password?token=...`.

---

### Що саме "підтасувати" для фронтендера (NextAuth tips):

1. **Формат відповіді:** При логіні повертайте не просто токен, а об'єкт:

```json
{
    "user": { "id": "guid", "email": "...", "name": "..." },
    "accessToken": "ey...",
    "refreshToken": "...",
    "expiresIn": 3600
}
```

_Це дозволить фронтендеру легко наповнювати `session` в NextAuth._ 2. **Snake_case або CamelCase:** Домовтеся про іменування полів (зазвичай JSON в C# — CamelCase, що ідеально для JS). 3. **Swagger:** Обов'язково налаштуйте Swagger з підтримкою Bearer токенів, щоб фронтендер міг тестувати запити без Postman. 4. **Error Codes:** Замість загальної "400 Bad Request", повертайте чіткі коди: `"EMAIL_NOT_CONFIRMED"`, `"INVALID_2FA_CODE"`, `"TOKEN_EXPIRED"`. Це дозволить NextAuth красиво відображати помилки на сторінці входу.

**Готова перша під-таска для Jira/Trello:**

> **Title:** Auth Infrastructure: Identity, JWT + Refresh Flow, Google Integration.
> **Description:** Налаштувати DB context для Identity, реалізувати видачу JWT (15 хв) та Refresh Token (7 днів) в БД PostgreSQL. Створити ендпоінти Login/Register/Refresh для інтеграції з NextAuth CredentialsProvider.
