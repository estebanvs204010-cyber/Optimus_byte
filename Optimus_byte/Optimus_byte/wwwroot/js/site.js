// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

function mostrarMenu() {
    const menu = document.getElementById("barraLateral");
    const contenido = document.getElementById("contenidoPrincipal");

    menu.classList.toggle("activa");
    contenido.classList.toggle("movido");
}

