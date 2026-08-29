namespace misabarber.DTOs;

// Lo que manda el navegador al suscribirse -- mismos nombres que usa
// PushSubscription.toJSON() de la Push API (endpoint / keys.p256dh /
// keys.auth), así el front no tiene que remapear nada antes de mandarlo
// (ver src/utils/pushNotifications.js).
public record SuscripcionPushCreateDto(string Endpoint, SuscripcionPushKeysDto Keys);
public record SuscripcionPushKeysDto(string P256dh, string Auth);

public record VapidPublicKeyDto(string PublicKey);
