namespace misabarber.Models;

// Un mensaje de chat entre un Cliente y el staff (Admin/Barbero) de SU
// barbería. Una sola conversación por Cliente -- no hay "temas" ni chats
// separados, es un chat de soporte simple, como el de MisaDesk. Sin
// navegación FK a Usuario a propósito (mismo criterio que CitaAuditoria
// en MisaBarberContext.OnModelCreating): es más un log de conversación
// que una relación estricta, así que los nombres van denormalizados
// (ClienteNombre, AutorNombre) en vez de requerir un join para listar
// conversaciones o mostrar el remitente de cada burbuja.
public class ChatMensaje
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid NegocioId { get; set; }
    public Negocio? Negocio { get; set; }

    // Usuario.Id del Cliente dueño de esta conversación -- tanto si el
    // mensaje lo escribió el cliente como si lo escribió el staff, esto
    // identifica DE QUÉ conversación es (el staff puede tener muchas
    // conversaciones abiertas, un cliente solo tiene la suya con su
    // barbería, ver ChatController).
    public Guid ClienteId { get; set; }
    public string ClienteNombre { get; set; } = "";

    // Quién escribió este mensaje puntual.
    public Guid AutorId { get; set; }
    public string AutorNombre { get; set; } = "";
    public string AutorRol { get; set; } = ""; // "Cliente" | "Admin" | "Barbero"

    public string Texto { get; set; } = "";
    public DateTime FechaEnvio { get; set; } = DateTime.UtcNow;

    // Leído del lado que NO lo escribió -- el propio autor ya lo "leyó"
    // por definición al mandarlo. Alimentan el badge de no-leídos del
    // sidebar (staff, ver Layout.jsx) y de la burbuja flotante (cliente,
    // ver ChatWidget.jsx).
    public bool LeidoPorStaff { get; set; }
    public bool LeidoPorCliente { get; set; }
}
