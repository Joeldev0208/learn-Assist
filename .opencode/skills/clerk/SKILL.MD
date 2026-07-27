---
name: clerk
description: Instrucciones y mejores prácticas para integrar, depurar y gestionar la autenticación de usuarios con Clerk Auth en aplicaciones web (Next.js, Express, React, etc.).
compatibility: opencode
metadata:
  version: "1.0.0"
  category: "authentication"
---

# Clerk Authentication Skill

Esta habilidad te dota de los conocimientos necesarios para implementar de forma segura la autenticación, gestión de sesiones y control de usuarios utilizando Clerk en cualquier proyecto de software.

## Cuándo usar esta habilidad
- Al añadir sistemas de inicio de sesión (Sign-In) o registro (Sign-Up).
- Al proteger rutas específicas, páginas o endpoints de API.
- Al interactuar con el SDK de Clerk (por ejemplo, `@clerk/nextjs` o `@clerk/express`).
- Al configurar Webhooks para sincronizar usuarios con bases de datos (Prisma, PostgreSQL, etc.).

## Directrices obligatorias de implementación

### 1. Variables de Entorno (Environment Variables)
Siempre verifica o solicita al usuario que configure las siguientes claves en su archivo `.env.local` o `.env`:
- `NEXT_PUBLIC_CLERK_PUBLISHABLE_KEY`: Clave pública para el frontend.
- `CLERK_SECRET_KEY`: Clave secreta para operaciones en el backend (¡Nunca la expongas!).

### 2. Middleware de Protección de Rutas (Patrón Moderno v5+)
Al proteger rutas, evita usar métodos obsoletos como `clerkAuthMiddleware`. En su lugar, implementa siempre `clerkMiddleware` combinado con `createRouteMatcher`:

```typescript
import { clerkMiddleware, createRouteMatcher } from '@clerk/nextjs/server';

// Define qué rutas requieren autenticación
const isProtectedRoute = createRouteMatcher(['/dashboard(.*)', '/forum(.*)']);

export default clerkMiddleware(async (auth, req) => {
  if (isProtectedRoute(req)) await auth.protect();
});

export const config = {
  matcher: [
    '/((?!_next|[^?]*\\.(?:html|css|js|gif|svg|png|webp|jpg|jpeg|webp|woff2?|ico|csv|docx|xlsx|zip|webmanifest)).*)',
    '/(api|trpc)(.*)',
  ],
};
```

### 3. Componentes de Flujo de Usuario
Utiliza los componentes nativos de Clerk para construir las interfaces visuales rápidamente:
- `<SignedIn>` y `<SignedOut>` para renderizar contenido condicional según el estado de la sesión.
- `<UserButton />` para mostrar el avatar del usuario y el menú de cierre de sesión.
- `<SignInButton />` y `<SignUpButton />` para botones de acción rápida.

## Comandos y Automatizaciones recomendadas
- Si te piden "añadir clerk", automatiza la instalación ejecutando `npm install @clerk/nextjs` (o el framework correspondiente) y genera el archivo `middleware.ts`.
- Si detectas errores de tipo `ClerkAuth`, revisa que el archivo raíz esté correctamente envuelto en el componente `<ClerkProvider>`.
