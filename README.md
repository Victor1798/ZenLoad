# 🧘 ZenLoad

> **Order in your downloads, peace in your workflow.**

**ZenLoad** es una herramienta nativa para Windows 11 que monitorea la carpeta de Descargas en segundo plano y organiza automáticamente cada archivo en su directorio correspondiente según su extensión. 

Construida con **.NET 8** y **WPF-UI**, ofrece una experiencia Fluent Design moderna con esquinas redondeadas, efectos Mica y un consumo mínimo de recursos.

---

## ✨ Características Principales

- ⚡ **Organización Automática en Tiempo Real:** Detecta cuando una descarga finaliza y la mueve instantáneamente a la carpeta asignada.
- 🎨 **Interfaz Nativa de Windows 11:** Diseñada con Fluent Design para integrarse perfectamente con el sistema operativo.
- ⚙️ **Reglas Personalizables:** Asigna rutas específicas para extensiones comunes (`.pdf`, `.docx`, `.zip`, `.exe`, etc.).
- 🤖 **Detección Dinámica de Formatos:** Si descarga una extensión desconocida (ej. `.bak`), ZenLoad te preguntará si deseas crear una nueva regla y subcarpeta automáticamente.
- 🔄 **Modo Reset y Guardado Rápido:** Restaura las rutas por defecto o guarda tus configuraciones con un solo clic.

---

## 🛠️ Requisitos e Instalación

- **Sistema Operativo:** Windows 11 (build 22000 o superior).
- **Runtime:** [.NET 8.0 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0).

### Instrucciones para Desarrolladores

```bash
# Clonar el repositorio
git clone [https://github.com/TU-USUARIO/ZenLoad.git](https://github.com/TU-USUARIO/ZenLoad.git)

# Entrar al directorio
cd ZenLoad

# Compilar y ejecutar
dotnet run --project ZenLoad