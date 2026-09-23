# ZenLoad

ZenLoad es una utilidad nativa para Windows 11 que vigila la carpeta
`Descargas` y organiza automaticamente los archivos segun su extension.

## Funcionalidades

- Mueve archivos nuevos a la carpeta configurada para su extension.
- Espera a que la descarga termine antes de mover el archivo.
- Procesa los archivos que ya existian en `Descargas` al iniciar.
- Permite agrupar extensiones en una sola regla, por ejemplo:
  `.doc, .docx, .xls`.
- Detecta extensiones desconocidas y permite crear una regla nueva.
- Permite activar o desactivar reglas temporalmente.
- Incluye pausa y reanudacion desde la ventana o la bandeja del sistema.
- Muestra notificaciones de Windows e historial de actividad.
- Valida las rutas de destino y registra errores de acceso o movimiento.
- Sigue automaticamente el tema claro u oscuro de Windows 11.
- Puede iniciar con Windows y permanecer oculto en la bandeja del sistema.

La configuracion se guarda en:

```text
%LOCALAPPDATA%\ZenLoad\config.json
```

## Requisitos

- Windows 11 de 64 bits.
- Para usar el instalador: no se necesita instalar .NET; la aplicacion incluye
  su propio runtime.
- Para ejecutar desde el codigo fuente: SDK de .NET 8.0 o superior.

## Instalacion recomendada

1. Abre el archivo `installer\ZenLoad-Setup.exe`.
2. Sigue el asistente de instalacion.
3. Abre ZenLoad desde el menu Inicio o desde el acceso directo del escritorio,
   si lo seleccionaste durante la instalacion.

El instalador coloca la aplicacion en la carpeta local de programas y crea los
accesos necesarios. Para cerrar completamente ZenLoad, haz clic derecho en su
icono de la bandeja del sistema y selecciona **Salir**. Al cerrar la ventana
normalmente solo se oculta y continua organizando archivos en segundo plano.

## Ejecutar el archivo directamente

Despues de publicar el proyecto, puedes abrir este ejecutable autonomo:

```text
publish\ZenLoad.exe
```

Tambien puedes abrir la version de desarrollo:

```text
bin\Debug\net8.0-windows\ZenLoad.exe
```

La version de desarrollo requiere el SDK o runtime de .NET 8 instalado. Si
ZenLoad ya esta abierto, cierralo desde el menu **Salir** de la bandeja antes
de compilar otra vez para evitar que el ejecutable quede bloqueado.

## Ejecutar desde el codigo fuente

```powershell
cd C:\Repo-GitHub\ZenLoad
dotnet restore
dotnet run --project ZenLoad.csproj
```

Para compilar una publicacion autonoma de Windows x64:

```powershell
.\scripts\Publish-ZenLoad.ps1
```

El resultado se genera en `publish\ZenLoad.exe`. Si Inno Setup esta instalado,
el mismo script tambien genera `installer\ZenLoad-Setup.exe`.

## Configuracion inicial

Al abrir la aplicacion, revisa las reglas y pulsa **Guardar**. Las extensiones
pueden escribirse juntas separadas por comas, espacios, punto y coma o saltos
de linea. Por ejemplo:

```text
.pdf, .docx, .txt
```

Para cada regla se puede seleccionar la carpeta de destino con el boton de
exploracion, evitando escribir la ruta manualmente. **Restablecer** devuelve
las reglas a la configuracion inicial.

## Estructura principal

- `Models/AppConfig.cs`: reglas y persistencia en `%LOCALAPPDATA%\ZenLoad\config.json`.
- `Models/ActivityEntry.cs`: entradas del historial de actividad.
- `Services/FolderMonitorService.cs`: `FileSystemWatcher`, escaneo inicial,
  espera de archivos y movimiento seguro.
- `ViewModels/MainViewModel.cs`: reglas observables y comandos de la interfaz.
- `Views/MainWindow.xaml`: interfaz Fluent basada en WPF-UI.
- `Assets/ZenLoad.ico`: icono integrado en el ejecutable y la bandeja del sistema.
- `installer/ZenLoad.iss`: configuracion del instalador de Inno Setup.
- `scripts/Publish-ZenLoad.ps1`: publicacion y creacion del instalador.
- `scripts/Generate-ZenLoadIcon.ps1`: regeneracion del icono en varios tamanos.

## Tecnologias

- C# y .NET 8
- WPF
- WPF-UI / Fluent Design
- Arquitectura MVVM
- `FileSystemWatcher`
- JSON para la configuracion local
