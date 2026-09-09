import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { App } from "./App";
import { SignalsProvider } from "./signals/SignalsProvider";
import "./styles.css";

const root = document.getElementById("root");

if (root === null) {
  throw new Error("index.html ne porte pas d'élément #root.");
}

createRoot(root).render(
  <StrictMode>
    <SignalsProvider>
      <App />
    </SignalsProvider>
  </StrictMode>,
);
