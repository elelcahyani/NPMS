let currentUser = {
  name: "R&D User",
  role: "R&D Team",
  isRd: true
};

let currentView = "list";
let currentProduct = null;
let activeProcessIndex = 0;
let productsData = [];

function element(tag, attributes = {}, children = []) {
  const item = document.createElement(tag);
  Object.entries(attributes).forEach(([name, value]) => {
    if (name === "className") {
      item.className = value;
    } else if (name === "textContent") {
      item.textContent = value ?? "";
    } else if (name.startsWith("on") && typeof value === "function") {
      item.addEventListener(name.slice(2), value);
    } else if (name === "checked" || name === "required" || name === "disabled" || name === "selected") {
      item[name] = Boolean(value);
    } else if (name === "htmlFor") {
      item.htmlFor = String(value);
    } else if (value !== null && value !== undefined) {
      item.setAttribute(name, String(value));
    }
  });
  children.filter(child => child !== null && child !== undefined).forEach(child => {
    item.append(child.nodeType ? child : document.createTextNode(String(child)));
  });
  return item;
}

function text(value, fallback = "") {
  return document.createTextNode(value === null || value === undefined || value === "" ? fallback : String(value));
}

function labelValue(label, value, className = "info-box") {
  return element("div", { className: "info-group" }, [
    element("div", { className: "info-label", textContent: label }),
    element("div", { className, textContent: value ?? "" })
  ]);
}

function card(title, children) {
  return element("div", { className: "card" }, [
    element("div", { className: "card-title", textContent: title }),
    ...children
  ]);
}

function button(label, className, handler) {
  return element("button", { className, type: "button", onclick: handler }, [text(label)]);
}

function getStatusBadge(status) {
  let cls = "badge-draft";
  if (status === "Released") cls = "badge-released";
  else if (status === "Obsolete") cls = "badge-obsolete";
  return element("span", { className: `badge ${cls}` }, [
    element("span", { style: "font-size:8px;", textContent: "●" }),
    text(` ${status ?? ""}`)
  ]);
}

function safeImagePath(path) {
  return typeof path === "string" && /^\/images\/[A-Za-z0-9._-]+$/.test(path)
    ? path
    : "/images/power_inductor.png";
}

document.addEventListener("DOMContentLoaded", () => {
  fetchProducts();
});

function toggleUserRole() {
  currentUser = currentUser.isRd
    ? { name: "User", role: "Non-R&D Department", isRd: false }
    : { name: "R&D User", role: "R&D Team", isRd: true };
  updateUserUI();
  if (currentView === "list") renderListView();
  else if (currentView === "detail" && currentProduct) renderDetailView(currentProduct.productId);
}

function updateUserUI() {
  document.getElementById("sidebar-user-name").textContent = currentUser.name;
  document.getElementById("sidebar-user-role").textContent = currentUser.role;
  document.getElementById("role-toggle-label").textContent = currentUser.isRd ? "Non-R&D" : "R&D Team";
}

async function fetchProducts(query = "", status = "", factory = "") {
  try {
    const url = `/api/products?query=${encodeURIComponent(query)}&status=${encodeURIComponent(status)}&factory=${encodeURIComponent(factory)}`;
    const res = await fetch(url);
    if (res.ok) {
      productsData = await res.json();
      if (currentView === "list") renderListView();
    }
  } catch (err) {
    console.error("Error fetching products:", err);
  }
}

function switchView(view, productId = null) {
  currentView = view;
  if (view === "list") fetchProducts();
  else if (view === "detail" && productId) renderDetailView(productId);
  else if (view === "form") renderFormView(productId);
}

function handleTopSearch(e) {
  if (e.key === "Enter" || e.type === "keyup") {
    if (currentView !== "list") currentView = "list";
    fetchProducts(e.target.value, getFilterVal("filter-status"), getFilterVal("filter-factory"));
  }
}

function getFilterVal(id) {
  const item = document.getElementById(id);
  return item ? item.value : "";
}

function applyFilters() {
  fetchProducts(
    document.getElementById("top-search-input").value,
    getFilterVal("filter-status"),
    getFilterVal("filter-factory")
  );
}

function clearFilters() {
  document.getElementById("top-search-input").value = "";
  ["filter-status", "filter-factory", "filter-part"].forEach((id, index) => {
    const item = document.getElementById(id);
    if (item) item.value = ["Status", "Factory", "Part Number"][index];
  });
  fetchProducts();
}

function renderListView() {
  const main = document.getElementById("main-content");
  const root = element("div");
  root.append(
    element("div", { className: "breadcrumb" }, [element("span", { textContent: "Dashboard" })]),
    element("div", { className: "page-header" }, [
      element("div", { className: "page-title-group" }, [
        element("h1", { textContent: "Product Database" }),
        element("p", { textContent: "Search and manage product information" })
      ]),
      currentUser.isRd ? button("+ Add New Product", "btn btn-primary", () => switchView("form")) : null
    ])
  );

  const partFilter = element("select", { id: "filter-part", className: "filter-select", onchange: applyFilters }, [
    element("option", { textContent: "Part Number" })
  ]);
  productsData.forEach(product => partFilter.append(element("option", {
    value: product.partNumber,
    textContent: product.partNumber
  })));

  const statusFilter = element("select", { id: "filter-status", className: "filter-select", onchange: applyFilters }, [
    element("option", { textContent: "Status" }),
    ...["Released", "Draft", "Obsolete", "Pending Review"].map(value =>
      element("option", { value, textContent: value }))
  ]);
  const factoryFilter = element("select", { id: "filter-factory", className: "filter-select", onchange: applyFilters }, [
    element("option", { textContent: "Factory" }),
    ...["Bintan", "Alpha Facility", "Beta Facility", "Gamma Facility", "Delta Facility", "Epsilon Facility"].map(value =>
      element("option", { value, textContent: value }))
  ]);
  root.append(element("div", { className: "filter-bar" }, [
    partFilter, statusFilter, factoryFilter,
    button("Clear Filters", "clear-filter-btn", clearFilters)
  ]));

  const body = element("tbody");
  if (productsData.length === 0) {
    body.append(element("tr", {}, [
      element("td", { colspan: "8", style: "text-align:center; padding: 40px; color: var(--text-muted);", textContent: "No products found matching criteria." })
    ]));
  } else {
    productsData.forEach(product => {
      const row = element("tr", { onclick: () => switchView("detail", product.productId) }, [
        element("td", { className: "pn-link", textContent: product.partNumber }),
        element("td", { style: "font-weight: 600;", textContent: product.productName }),
        element("td", { textContent: product.productType }),
        element("td", {}, [getStatusBadge(product.status)]),
        element("td", { textContent: product.currentRevision }),
        element("td", { textContent: product.factory }),
        element("td", { textContent: product.productionLine }),
        element("td", { textContent: new Date(product.lastModified).toLocaleDateString() })
      ]);
      body.append(row);
    });
  }
  root.append(element("div", { className: "data-table-container" }, [
    element("table", { className: "data-table" }, [
      element("thead", {}, [element("tr", {}, [
        ...["PART NUMBER", "PRODUCT NAME", "PRODUCT TYPE", "STATUS", "CURRENT REV", "FACTORY", "PROD LINE", "LAST MODIFIED"]
          .map(value => element("th", { textContent: value }))
      ])]),
      body
    ])
  ]));
  main.replaceChildren(root);
}

async function renderDetailView(productId) {
  try {
    const res = await fetch(`/api/products/${productId}`);
    if (!res.ok) return;
    currentProduct = await res.json();
    activeProcessIndex = 0;
    const p = currentProduct;
    const spec = p.specification || {};
    const mfg = p.manufacturingInfo || {};
    const processes = p.processes || [];
    const docs = p.documents || [];
    const main = document.getElementById("main-content");
    const root = element("div");

    const back = () => switchView("list");
    root.append(
      element("div", { className: "breadcrumb" }, [
        element("a", { href: "#", onclick: e => { e.preventDefault(); back(); }, textContent: "Dashboard" }),
        text(" › Search Results › Product Dashboard")
      ]),
      element("div", { style: "margin-bottom: 16px;" }, [
        element("a", { href: "#", onclick: e => { e.preventDefault(); back(); }, textContent: "← Back to Search Results" })
      ]),
      element("div", { className: "page-header" }, [
        element("div", { className: "page-title-group" }, [
          element("h1", { textContent: String(p.productName || "").toUpperCase() })
        ]),
        currentUser.isRd
          ? button("✏️ Edit Product", "btn btn-outline", () => switchView("form", p.productId))
          : element("span", { className: "badge badge-readonly", textContent: "👁️ Read-only Access" })
      ])
    );

    const image = element("img", {
      src: safeImagePath(p.imagePath),
      className: "product-img-thumb",
      alt: "Product Image"
    });
    image.addEventListener("error", () => { image.src = "/images/power_inductor.png"; }, { once: true });
    const productCard = card("Product Information", [
      element("div", { className: "product-info-grid" }, [
        image,
        element("div", {}, [
          labelValue("PART NUMBER", p.partNumber),
          labelValue("PRODUCT TYPE", p.productType),
          labelValue("CURRENT REVISION", p.currentRevision)
        ]),
        element("div", {}, [
          labelValue("PRODUCT NAME", p.productName),
          labelValue("STATUS", null, "info-box")
        ])
      ]),
      labelValue("DESCRIPTION", p.description, "info-box")
    ]);
    productCard.querySelectorAll(".info-box")[4].replaceChildren(getStatusBadge(p.status));

    const manufacturingCard = card("Manufacturing Information", [
      element("div", { className: "form-grid" }, [
        labelValue("FACTORY", mfg.factory, "info-box"),
        labelValue("PRODUCTION LINE", mfg.productionLine, "info-box")
      ]),
      labelValue("PRODUCTION TYPE", mfg.productionType, "info-box"),
      labelValue("MANUFACTURING NOTES", mfg.manufacturingNotes, "info-box")
    ]);

    const processTabs = element("div", { className: "process-tabs" });
    processes.forEach((process, index) => processTabs.append(element("div", {
      className: `process-tab ${index === activeProcessIndex ? "active" : ""}`,
      onclick: () => selectProcessTab(index),
      textContent: process.processName
    })));
    const processContent = element("div", { id: "process-tab-content" });
    processContent.append(renderProcessTabContent(processes[activeProcessIndex]));
    const processCard = card("Process Flow", [processTabs, processContent]);

    const documentBody = element("tbody");
    if (docs.length === 0) {
      documentBody.append(element("tr", {}, [
        element("td", { colspan: "5", style: "text-align:center; padding: 20px; color: var(--text-muted);", textContent: "No supporting documents attached." })
      ]));
    } else {
      docs.forEach(doc => {
        const downloadUrl = `/api/documents/${encodeURIComponent(doc.documentId)}/download`;
        const actions = [
          element("a", { href: downloadUrl, target: "_blank", className: "pn-link", textContent: "Open" }),
          element("a", { href: downloadUrl, className: "pn-link", download: "", textContent: "Download" })
        ];
        if (currentUser.isRd) actions.push(element("a", {
          href: "#",
          style: "color:#ef4444; margin-left:12px; font-weight:500;",
          onclick: e => { e.preventDefault(); deleteDoc(doc.documentId); },
          textContent: "Delete"
        }));
        documentBody.append(element("tr", {}, [
          element("td", { style: "font-weight:600;", textContent: doc.documentName }),
          element("td", { textContent: doc.documentType }),
          element("td", { textContent: doc.revision }),
          element("td", { textContent: doc.fileName }),
          element("td", {}, [element("span", {}, actions)])
        ]));
      });
    }
    const documentsCard = card("Supporting Documents", [
      element("div", { style: "display:flex; justify-content:space-between; align-items:center; margin-bottom: 16px;" }, [
        element("div", { className: "card-title", style: "margin:0;", textContent: "Supporting Documents" }),
        currentUser.isRd ? button("+ Upload Document", "btn btn-outline btn-sm", () => openModal("upload-modal")) : null
      ]),
      element("div", { className: "data-table-container" }, [
        element("table", { className: "data-table" }, [
          element("thead", {}, [element("tr", {}, [
            ...["DOCUMENT NAME", "DOCUMENT TYPE", "REVISION", "FILE NAME", "ACTIONS"].map(value =>
              element("th", { textContent: value }))
          ])]),
          documentBody
        ])
      ])
    ]);

    const specificationCard = card("Product Specification", [
      element("div", { className: "spec-group-title", textContent: "MATERIAL" }),
      specRow("Material", spec.material, "Ferrite"),
      element("div", { className: "spec-group-title", textContent: "DIMENSIONS" }),
      specRow("Length", spec.length, "25 mm"),
      specRow("Width", spec.width, "15 mm"),
      specRow("Height", spec.height, "10 mm"),
      specRow("Tolerance", spec.tolerance, "±0.1 mm"),
      element("div", { className: "spec-group-title", textContent: "ELECTRICAL" }),
      specRow("Inductance", spec.inductance, "10 µH"),
      specRow("Rated Current", spec.ratedCurrent, "5 A"),
      specRow("DCR", spec.dcr, "0.2 Ω"),
      element("div", { className: "spec-group-title", textContent: "OPERATING CONDITIONS" }),
      specRow("Operating Temp", spec.operatingTemperature, "-40 to 125 °C")
    ]);
    root.append(element("div", { className: "detail-grid" }, [
      element("div", {}, [productCard, manufacturingCard, processCard, documentsCard]),
      element("div", {}, [specificationCard])
    ]));
    main.replaceChildren(root);
  } catch (err) {
    console.error("Error loading detail view:", err);
  }
}

function specRow(label, value, fallback) {
  return element("div", { className: "spec-row" }, [
    element("span", { className: "label", textContent: label }),
    element("span", { className: "value", textContent: value || fallback })
  ]);
}

function selectProcessTab(index) {
  activeProcessIndex = index;
  if (!currentProduct?.processes) return;
  document.querySelectorAll(".process-tab").forEach((tab, tabIndex) => {
    tab.classList.toggle("active", tabIndex === index);
  });
  const content = document.getElementById("process-tab-content");
  if (content) content.replaceChildren(renderProcessTabContent(currentProduct.processes[index]));
}

function renderProcessTabContent(process) {
  if (!process) {
    return element("div", { style: "padding: 20px; color: var(--text-muted);", textContent: "No process details available." });
  }
  const params = process.parameters || [];
  const body = element("tbody");
  if (params.length === 0) {
    body.append(element("tr", {}, [
      element("td", { colspan: "3", style: "text-align:center; padding:16px; color:var(--text-muted);", textContent: "No parameters defined." })
    ]));
  } else {
    params.forEach(parameter => body.append(element("tr", {}, [
      element("td", { style: "font-weight:600;", textContent: parameter.parameterName }),
      element("td", { textContent: parameter.parameterValue }),
      element("td", { textContent: parameter.unit })
    ])));
  }
  return element("div", {}, [
    element("div", { className: "process-details-card" }, [
      element("div", { style: "font-weight:700; margin-bottom:12px; font-size:14px;", textContent: "Process Details" }),
      element("div", { className: "form-grid" }, [
        labelValue("PROCESS NAME", process.processName),
        labelValue("MACHINE", process.machineName, "info-box")
      ]),
      labelValue("TOOLING", process.toolingName || "Tool-23"),
      labelValue("PROCESS DESCRIPTION", process.processDescription, "info-box")
    ]),
    element("div", { className: "data-table-container" }, [
      element("table", { className: "data-table" }, [
        element("thead", {}, [element("tr", {}, [
          ...["PARAMETER", "VALUE", "UNIT"].map(value => element("th", { textContent: value }))
        ])]),
        body
      ])
    ])
  ]);
}

function inputField(id, value, label, type = "text") {
  return element("div", { className: "form-group" }, [
    element("label", { htmlFor: id, textContent: label }),
    element("input", { id, className: "form-control", type, value: value ?? "" })
  ]);
}

function selectField(id, value, label, options) {
  const select = element("select", { id, className: "form-control" });
  options.forEach(option => select.append(element("option", {
    value: option,
    selected: option === value,
    textContent: option
  })));
  return element("div", { className: "form-group" }, [
    element("label", { htmlFor: id, textContent: label }),
    select
  ]);
}

function textAreaField(id, value, label) {
  return element("div", { className: "form-group" }, [
    element("label", { htmlFor: id, textContent: label }),
    element("textarea", { id, className: "form-control" }, [text(value)])
  ]);
}

async function renderFormView(productId = null) {
  let product = {
    partNumber: "",
    productName: "",
    productType: "Inductor",
    status: "Draft",
    currentRevision: "Revision A",
    description: "",
    specification: { material: "Ferrite", length: "25 mm", width: "15 mm", height: "10 mm", tolerance: "±0.1 mm", inductance: "10 µH", ratedCurrent: "5 A", dcr: "0.2 Ω", operatingTemperature: "-40 to 125 °C" },
    manufacturingInfo: { factory: "Bintan", productionLine: "Line 03", productionType: "Automated Assembly", manufacturingNotes: "" }
  };
  if (productId) {
    const res = await fetch(`/api/products/${productId}`);
    if (res.ok) product = await res.json();
  }
  const spec = product.specification || {};
  const mfg = product.manufacturingInfo || {};
  const main = document.getElementById("main-content");
  const root = element("div");
  root.append(
    element("div", { className: "breadcrumb" }, [
      element("a", { href: "#", onclick: e => { e.preventDefault(); switchView("list"); }, textContent: "Dashboard" }),
      element("span", { textContent: productId ? " › Edit Product" : " › Add New Product" })
    ]),
    element("div", { className: "page-header" }, [
      element("div", { className: "page-title-group" }, [
        element("h1", { textContent: productId ? `Edit Product: ${product.partNumber || ""}` : "Add New Product" })
      ]),
      element("div", { style: "display:flex; gap:12px;" }, [
        button("Cancel", "btn btn-outline", () => switchView("list")),
        button("Save as Draft", "btn btn-secondary", () => saveProductForm(productId, "Draft")),
        button("Save Product", "btn btn-primary", () => saveProductForm(productId, "Released"))
      ])
    ])
  );
  const info = card("Product Information", [
    element("div", { className: "form-grid" }, [
      inputField("form-pn", product.partNumber, "Part Number"),
      inputField("form-name", product.productName, "Product Name")
    ]),
    element("div", { className: "form-grid" }, [
      selectField("form-type", product.productType, "Product Type", ["Inductor", "Transformer", "Sensor", "Choke"]),
      selectField("form-status", product.status, "Status", ["Draft", "Released", "Obsolete", "Pending Review"])
    ]),
    inputField("form-rev", product.currentRevision || "Revision A", "Current Revision"),
    textAreaField("form-desc", product.description, "Description")
  ]);
  const manufacturing = card("Manufacturing Information", [
    element("div", { className: "form-grid" }, [
      selectField("form-factory", mfg.factory || "Bintan", "Factory", ["Bintan", "Alpha Facility", "Beta Facility"]),
      inputField("form-line", mfg.productionLine || "Line 03", "Production Line")
    ]),
    inputField("form-prodtype", mfg.productionType || "Automated Assembly", "Production Type"),
    textAreaField("form-mfgnotes", mfg.manufacturingNotes, "Manufacturing Notes")
  ]);
  const specifications = card("Product Specification", [
    inputField("spec-material", spec.material || "Ferrite", "Material"),
    inputField("spec-length", spec.length || "25 mm", "Length"),
    inputField("spec-width", spec.width || "15 mm", "Width"),
    inputField("spec-height", spec.height || "10 mm", "Height"),
    inputField("spec-tolerance", spec.tolerance || "±0.1 mm", "Tolerance"),
    inputField("spec-inductance", spec.inductance || "10 µH", "Inductance"),
    inputField("spec-current", spec.ratedCurrent || "5 A", "Rated Current"),
    inputField("spec-dcr", spec.dcr || "0.2 Ω", "DCR"),
    inputField("spec-temp", spec.operatingTemperature || "-40 to 125 °C", "Operating Temperature")
  ]);
  root.append(element("div", { className: "detail-grid" }, [
    element("div", {}, [info, manufacturing]),
    element("div", {}, [specifications])
  ]));
  main.replaceChildren(root);
}

async function saveProductForm(productId, targetStatus) {
  const value = id => document.getElementById(id).value;
  const pn = value("form-pn").trim();
  const name = value("form-name").trim();
  if (!pn || !name) {
    alert("Part Number and Product Name are required.");
    return;
  }
  const dto = {
    productId: productId || 0,
    partNumber: pn,
    productName: name,
    productType: value("form-type"),
    status: targetStatus || value("form-status"),
    currentRevision: value("form-rev"),
    description: value("form-desc"),
    imagePath: "/images/power_inductor.png",
    specification: {
      material: value("spec-material"), length: value("spec-length"), width: value("spec-width"),
      height: value("spec-height"), tolerance: value("spec-tolerance"), inductance: value("spec-inductance"),
      ratedCurrent: value("spec-current"), dcr: value("spec-dcr"), operatingTemperature: value("spec-temp")
    },
    manufacturingInfo: {
      factory: value("form-factory"), productionLine: value("form-line"),
      productionType: value("form-prodtype"), manufacturingNotes: value("form-mfgnotes")
    }
  };
  try {
    const url = productId ? `/api/products/${productId}` : "/api/products";
    const res = await fetch(url, {
      method: productId ? "PUT" : "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(dto)
    });
    if (res.ok) {
      const saved = await res.json();
      alert("Product saved successfully!");
      switchView("detail", saved.productId);
    } else {
      alert("Failed to save product.");
    }
  } catch (err) {
    console.error("Save error:", err);
  }
}

function openModal(id) {
  document.getElementById(id).classList.add("active");
}

function closeModal(id) {
  document.getElementById(id).classList.remove("active");
}

async function submitDocumentUpload() {
  if (!currentProduct) return;
  const name = document.getElementById("doc-name-input").value.trim();
  const type = document.getElementById("doc-type-input").value;
  const rev = document.getElementById("doc-rev-input").value.trim();
  const fileInput = document.getElementById("doc-file-input");
  if (!name || fileInput.files.length === 0) {
    alert("Document name and file selection are required.");
    return;
  }
  const formData = new FormData();
  formData.append("documentName", name);
  formData.append("documentType", type);
  formData.append("revision", rev);
  formData.append("file", fileInput.files[0]);
  try {
    const res = await fetch(`/api/products/${currentProduct.productId}/documents`, {
      method: "POST",
      body: formData
    });
    if (res.ok) {
      closeModal("upload-modal");
      renderDetailView(currentProduct.productId);
    } else {
      alert("Failed to upload document.");
    }
  } catch (err) {
    console.error("Upload document error:", err);
  }
}

async function deleteDoc(docId) {
  if (!confirm("Are you sure you want to delete this document?")) return;
  try {
    const res = await fetch(`/api/documents/${encodeURIComponent(docId)}`, { method: "DELETE" });
    if (res.ok) renderDetailView(currentProduct.productId);
  } catch (err) {
    console.error("Delete doc error:", err);
  }
}
