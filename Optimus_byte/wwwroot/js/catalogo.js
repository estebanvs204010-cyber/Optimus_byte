// Script cliente para el catálogo de repuestos.
// Añadido: carrito en localStorage, actualización de badges y panel,
// y evitar que aparezca el texto ALT cuando la imagen falla.

document.addEventListener('DOMContentLoaded', () => {
    const grid = document.getElementById('productosGrid');
    const searchInp = document.getElementById('searchInp');
    const badgeCount = document.getElementById('badgeCount');
    const sbCount = document.getElementById('sbCount');
    const totalVal = document.getElementById('totalVal');
    const carritoPanel = document.getElementById('carritoPanel');
    let productos = [];
    let filtro = 'todos';
    let orden = 'nombre';

    // --- Cart helpers (localStorage) ---
    function loadCart() {
        try {
            return JSON.parse(localStorage.getItem('ob_cart') || '[]');
        } catch {
            return [];
        }
    }
    function saveCart(cart) {
        localStorage.setItem('ob_cart', JSON.stringify(cart));
        updateCartBadges(cart);
        renderCarritoPanel(cart);
    }
    function updateCartBadges(cart) {
        const totalItems = cart.reduce((s, it) => s + (it.cantidad||0), 0);
        if (badgeCount) badgeCount.textContent = totalItems;
        if (sbCount) sbCount.textContent = totalItems;
        if (totalVal) {
            const total = cart.reduce((s, it) => s + (it.precio || 0) * (it.cantidad || 0), 0);
            totalVal.textContent = total.toLocaleString('es-CO', { maximumFractionDigits: 0 });
        }
    }
    function renderCarritoPanel(cart) {
        if (!carritoPanel) return;
        let list = carritoPanel.querySelector('#carritoItems');
        if (!list) {
            list = document.createElement('div');
            list.id = 'carritoItems';
            list.style.padding = '12px';
            carritoPanel.insertBefore(list, carritoPanel.querySelector('.cat-carrito-total'));
        }
        list.innerHTML = '';
        if (cart.length === 0) {
            list.innerHTML = '<div style="color:#64748b;padding:8px;">No hay artículos en la cotización.</div>';
            return;
        }
        for (const it of cart) {
            const row = document.createElement('div');
            row.className = 'cat-carrito-item';
            row.innerHTML = `
                <div style="display:flex;gap:10px;align-items:center;">
                    <div style="width:40px;height:40px;flex-shrink:0;">
                        <img src="${escapeHtml(it.imagen||'/img/defaults/repuesto.png')}" style="width:40px;height:40px;object-fit:cover;" onerror="this.src='/img/defaults/repuesto.png'"/>
                    </div>
                    <div style="font-size:.82rem;color:#1a2332;">${escapeHtml(it.nombre)}</div>
                </div>
                <div style="display:flex;align-items:center;gap:8px;">
                    <div style="font-weight:700;color:#d97706;">${Number(it.precio).toLocaleString('es-CO')}</div>
                    <div>
                        <button class="carrito-x" data-id="${it.id}" style="background:none;border:none;color:#dc2626;cursor:pointer;">✕</button>
                    </div>
                </div>
            `;
            list.appendChild(row);
        }

        // attach remove handlers
        list.querySelectorAll('.carrito-x').forEach(btn => {
            btn.addEventListener('click', (e) => {
                const id = Number(btn.getAttribute('data-id'));
                const cart = loadCart().filter(x => x.id !== id);
                saveCart(cart);
            });
        });
    }

    // --- Fetch & render productos ---
    async function cargarProductos() {
        try {
            const res = await fetch('/Inventario/GetProductos');
            if (!res.ok) throw new Error('Error al obtener productos');
            productos = await res.json();
            renderProductos(productos);
            // inicializar carrito UI
            const cart = loadCart();
            updateCartBadges(cart);
            renderCarritoPanel(cart);
        } catch (e) {
            console.error('GetProductos:', e);
            if (grid) grid.innerHTML = '<p style="color:#666;padding:24px;">No se pudieron cargar los productos. Revisa la consola.</p>';
        }
    }

    function normalizarCategoria(c) {
        if (!c) return '';
        return c.toLowerCase().normalize('NFD').replace(/[\u0300-\u036f]/g, '').replace(/\s+/g, '');
    }
    function bgClassForCategoria(cat) {
        switch (normalizarCategoria(cat)) {
            case 'motor': return 'bg-motor';
            case 'frenos': return 'bg-frenos';
            case 'filtros': return 'bg-filtros';
            case 'electrico':
            case 'sistemaelectrico': return 'bg-electrico';
            case 'suspension': return 'bg-suspension';
            case 'lubricantes':
            case 'lubricante': return 'bg-lubricante';
            default: return '';
        }
    }

    function renderProductos(list) {
        if (!grid) return;
        const filtrados = list
            .filter(p => filtro === 'todos' || (p.categoria || '').toLowerCase() === filtro.toLowerCase())
            .filter(p => {
                const q = (searchInp?.value || '').trim().toLowerCase();
                if (!q) return true;
                return (p.nombre || '').toLowerCase().includes(q) ||
                       (p.referencia || '').toLowerCase().includes(q) ||
                       (p.descripcion || '').toLowerCase().includes(q);
            });

        if (orden === 'precio-asc') filtrados.sort((a,b)=>a.precio-b.precio);
        else if (orden === 'precio-desc') filtrados.sort((a,b)=>b.precio-a.precio);
        else filtrados.sort((a,b)=> (a.nombre||'').localeCompare(b.nombre||''));

        if (filtrados.length === 0) {
            grid.innerHTML = '<div style="padding:24px;color:#666;">No hay productos que coincidan.</div>';
            return;
        }

        grid.innerHTML = '';
        for (const p of filtrados) {
            const id = p.id;
            const nombre = p.nombre || '';
            const referencia = p.referencia || '';
            const descripcion = p.descripcion || '';
            const categoria = p.categoria || '';
            const marca = p.marca || 'CHEVROLET';
            const precio = Number(p.precio || 0);
            const stock = Number(p.stock || 0);
            const imagen = p.imagenUrl || '/img/defaults/repuesto.png';
            const badgeClass = stock === 0 ? 'agotado' : (stock <= (p.stockMinimo||0) ? 'bajo' : '');
            const badgeTxt = stock === 0 ? 'Agotado' : (stock <= (p.stockMinimo||0) ? 'Stock bajo' : 'Disponible');
            const bgClass = bgClassForCategoria(categoria);

            const card = document.createElement('div');
            card.className = 'cat-card';
            card.innerHTML = `
                <div class="cat-card-img ${bgClass}">
                    <img src="${escapeHtml(imagen)}" alt="" onerror="this.onerror=null;this.src='/img/defaults/repuesto.png';this.alt='';"/>
                    <div class="cat-badge ${badgeClass}">${badgeTxt}</div>
                </div>
                <div class="cat-card-body">
                    <div class="cat-card-nombre">${escapeHtml(nombre)}</div>
                    <div class="cat-card-marca">
                        <span class="chip-marca">🏷 ${escapeHtml(marca)}</span>
                        <span style="margin:0 8px;color:#9ca3af">·</span>
                        <span style="font-size:.72rem;color:#64748b">${escapeHtml(categoria)}</span>
                    </div>
                    <div class="cat-card-desc">${escapeHtml(descripcion)}</div>
                    <div class="cat-card-precio">${precio.toLocaleString('es-CO')} <span>COP</span></div>
                    <div class="cat-card-btns">
                        <a class="cat-btn-detalles" href="/Inventario/Detalles/${id}">VER DETALLES</a>
                        <button class="cat-btn-agregar" data-id="${id}" ${stock===0? 'disabled' : ''}>
                            + COTIZAR
                        </button>
                    </div>
                </div>
            `;
            grid.appendChild(card);
        }

        // Attach agregar handlers (delegación simple)
        grid.querySelectorAll('.cat-btn-agregar').forEach(btn => {
            btn.addEventListener('click', () => {
                const id = Number(btn.getAttribute('data-id'));
                agregarCotizacion(id);
            });
        });
    }

    // --- carrito: agregar item ---
    function agregarCotizacion(id) {
        const prod = productos.find(p => Number(p.id) === Number(id));
        if (!prod) return;
        const cart = loadCart();
        const existing = cart.find(x => x.id === prod.id);
        if (existing) {
            existing.cantidad = (existing.cantidad || 1) + 1;
        } else {
            cart.push({
                id: prod.id,
                nombre: prod.nombre,
                precio: Number(prod.precio),
                cantidad: 1,
                imagen: prod.imagenUrl || '/img/defaults/repuesto.png'
            });
        }
        saveCart(cart);
        // feedback visual mínimo
        showTemporalMensaje('+ agregado a cotización');
    }

    function showTemporalMensaje(text) {
        const m = document.createElement('div');
        m.textContent = text;
        m.style.position = 'fixed';
        m.style.right = '18px';
        m.style.bottom = '18px';
        m.style.background = '#1e3a8a';
        m.style.color = '#fff';
        m.style.padding = '10px 14px';
        m.style.borderRadius = '8px';
        m.style.zIndex = 9999;
        document.body.appendChild(m);
        setTimeout(()=> m.remove(), 1400);
    }

    // funciones expuestas para la UI (ya usadas en el HTML)
    window.filtrar = (categoria, el) => {
        filtro = (categoria || 'todos').toLowerCase();
        document.querySelectorAll('.cat-sb-cat').forEach(n=>n.classList.remove('on'));
        if (el) el.classList.add('on');
        renderProductos(productos);
    };
    window.buscar = () => renderProductos(productos);
    window.ordenar = (v) => { orden = v; renderProductos(productos); };
    window.toggleCarrito = () => {
        if (!carritoPanel) return;
        carritoPanel.style.display = carritoPanel.style.display === 'none' ? 'block' : 'none';
    };

    // util
    function escapeHtml(s){ return String(s||'').replace(/[&<>"']/g, c=>({ '&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;' })[c]); }

    // iniciar
    cargarProductos();
});