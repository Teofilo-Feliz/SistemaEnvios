import { defineConfig, loadEnv } from "vite";
import vue from "@vitejs/plugin-vue";
import { fileURLToPath, URL } from "node:url";

// Estas dos deciden si hay autenticación: authService las evalúa en un ternario, así que si
// llegan vacías el bundler resuelve la rama muerta y UserManager desaparece del bundle. El
// resultado es una imagen que construye bien, arranca sana, pasa el healthcheck y muestra el
// botón de login deshabilitado. Como import.meta.env se resuelve al construir, no hay variable
// de entorno que lo arregle después: hay que rehacer la imagen. Mejor romper el build.
const REQUERIDAS = ["VITE_AUTH_AUTHORITY", "VITE_AUTH_CLIENT_ID"];

export default defineConfig(({ command, mode }) => {
  const raiz = fileURLToPath(new URL(".", import.meta.url));

  if (command === "build") {
    const env = loadEnv(mode, raiz, "VITE_");
    const faltantes = REQUERIDAS.filter((clave) => !env[clave]?.trim());
    if (faltantes.length > 0) {
      throw new Error(
        `Faltan variables para construir en modo '${mode}': ${faltantes.join(", ")}. ` +
          `Revise .env.${mode}. Cuidado con las variables de entorno vacías: tienen ` +
          `prioridad sobre los archivos y anulan lo que declaren.`,
      );
    }
  }

  return {
    plugins: [vue()],
    resolve: { alias: { "@": fileURLToPath(new URL("./src", import.meta.url)) } },
    server: { port: 5173 },
  };
});
