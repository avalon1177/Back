Оскільки бекенд-таска вже сформована під роботу з **NextAuth.js (Auth.js)**, фронтенд-таска має фокусуватися на створенні кастомних сторінок, управлінні сесіями та обробці складних сценаріїв (2FA, Refresh Tokens).

Ось детальний опис таски для фронтенд-розробника.

---

### Task: Auth Implementation (Next.js + NextAuth.js)

**Мета:** Реалізувати клієнтську частину аутентифікації, інтегровану з кастомним .NET API.

#### 1. Налаштування NextAuth Configuration (`[...nextauth].ts`)

-   **Credentials Provider:** Налаштувати логіку в `authorize`. При відправці email/password робити запит на бекенд `POST /api/auth/login`. Отримані `accessToken` та `refreshToken` записувати в `JWT` об’єкт NextAuth.
-   **Google Provider:** Налаштувати авторизацію через Google. Після отримання токена від Google, прокидати його на бекенд `POST /api/auth/external-login` для синхронізації профілю.
-   **Session & JWT Callbacks:**
-   Додати `accessToken` та `user info` в об’єкт сесії, щоб вони були доступні через `useSession()`.
-   **Refresh Token Rotation:** Реалізувати логіку в `jwt` callback: якщо термін дії `accessToken` вичерпано, автоматично викликати бекенд `POST /api/auth/refresh`.

#### 2. Створення UI (Pages & Components)

-   **Sign-in Page:** Кастомна сторінка входу (Email/Password + кнопка Google).
-   **Sign-up Page:** Форма реєстрації з валідацією (рекомендую **Zod** + **React Hook Form**).
-   **2FA Verification Step:** Створити проміжний екран для введення OTP-коду, якщо бекенд повернув статус `202 Accepted` (Require2FA).
-   **Password Recovery:** Сторінки "Забув пароль" (введення email) та "Новий пароль" (введення пароля з токеном з URL).

#### 3. Middleware & Guards

-   Налаштувати `middleware.ts` для захисту роутів (наприклад, `/profile`, `/orders`, `/admin` мають бути доступні лише авторизованим користувачам).
-   Реалізувати редирект на сторінку `/login`, якщо сесія відсутня.

#### 4. Обробка станів та помилок

-   Виведення повідомлень від бекенда: "Invalid credentials", "Email not confirmed", "Server error".
-   Відображення Loading-стейтів на кнопках під час запитів.

---

### Типовий Stack для фронтендера під цей бек:

1. **Framework:** Next.js (App Router).
2. **Auth:** NextAuth.js (v4 або v5/Auth.js).
3. **Validation:** Zod (ідеально мапиться на FluentValidation з бекенду).
4. **UI:** Tailwind CSS + Shadcn UI (швидкий старт для форм).

---

### Що фронтендер має запитати у бекендера (Checklist):

-   «Яка структура об'єкта `User` приходить після логіну?»
-   «Який час життя `accessToken` (щоб я налаштував таймер оновлення)?»
-   «Чи передавати мені `RefreshToken` у заголовках чи в тілі запиту при оновленні?»
-   «Який формат помилок (Error Response)?»

**Готова коротка назва для таски в Jira:**

> **Title:** Frontend Auth: NextAuth integration with custom .NET API (JWT, Google, 2FA).
> **Description:** Реалізувати повний цикл аутентифікації на фронтенді: логін/реєстрація, обробка Refresh Tokens через callbacks, кастомна сторінка 2FA та інтеграція Google Provider з бекенд-валідацією.

Чи хочеш, щоб я підготував **приклад коду для `[...nextauth].ts**`, який показує, як саме передавати JWT з бекенда в сесію Next.js?
