import type { MessageTree } from "./types";

export const enErrorMessages: MessageTree = {
  subscription: {
    booking_limit_reached:
      "This business reached its {limit} booking limit for this month. Upgrade to Pro for unlimited bookings.",
    multi_location_required:
      "Multi-location requires the Business plan. Upgrade to add more branches.",
  },
  booking: {
    slot_expired: "That time slot has passed. Please choose a new time.",
    slot_unavailable: "That time slot is no longer available.",
    slot_locked: "That time slot is being booked. Try again.",
  },
  auth: {
    required: "Authentication is required.",
    customer_required: "Sign in to complete this action.",
    business_access_denied: "You do not have access to this business.",
  },
  validation: "Please check your input and try again.",
  forbidden: "You do not have permission to perform this action.",
  conflict: "This action could not be completed because of a conflict.",
  not_found: "{resource} was not found.",
  internal: {
    server_error: "Something went wrong. Please try again later.",
  },
};

export const frErrorMessages: MessageTree = {
  subscription: {
    booking_limit_reached:
      "Cette entreprise a atteint sa limite de {limit} réservations ce mois-ci. Passez à Pro pour des réservations illimitées.",
    multi_location_required:
      "Les emplacements multiples nécessitent le forfait Business. Passez à un forfait supérieur pour ajouter des succursales.",
  },
  booking: {
    slot_expired: "Ce créneau horaire est passé. Veuillez en choisir un autre.",
    slot_unavailable: "Ce créneau horaire n'est plus disponible.",
    slot_locked: "Ce créneau est en cours de réservation. Réessayez.",
  },
  auth: {
    required: "Authentification requise.",
    customer_required: "Connectez-vous pour effectuer cette action.",
    business_access_denied: "Vous n'avez pas accès à cette entreprise.",
  },
  validation: "Vérifiez vos informations et réessayez.",
  forbidden: "Vous n'avez pas l'autorisation d'effectuer cette action.",
  conflict: "Cette action n'a pas pu être effectuée en raison d'un conflit.",
  not_found: "{resource} est introuvable.",
  internal: {
    server_error: "Une erreur s'est produite. Veuillez réessayer plus tard.",
  },
};

export const esErrorMessages: MessageTree = {
  subscription: {
    booking_limit_reached:
      "Este negocio alcanzó su límite de {limit} reservas este mes. Actualiza a Pro para reservas ilimitadas.",
    multi_location_required:
      "Varias ubicaciones requiere el plan Business. Actualiza para añadir más sucursales.",
  },
  booking: {
    slot_expired: "Ese horario ya pasó. Elige otro.",
    slot_unavailable: "Ese horario ya no está disponible.",
    slot_locked: "Ese horario se está reservando. Inténtalo de nuevo.",
  },
  auth: {
    required: "Se requiere autenticación.",
    customer_required: "Inicia sesión para completar esta acción.",
    business_access_denied: "No tienes acceso a este negocio.",
  },
  validation: "Revisa tu información e inténtalo de nuevo.",
  forbidden: "No tienes permiso para realizar esta acción.",
  conflict: "No se pudo completar esta acción debido a un conflicto.",
  not_found: "No se encontró {resource}.",
  internal: {
    server_error: "Algo salió mal. Inténtalo de nuevo más tarde.",
  },
};

export const ptErrorMessages: MessageTree = {
  subscription: {
    booking_limit_reached:
      "Este negócio atingiu o limite de {limit} reservas neste mês. Atualize para Pro para reservas ilimitadas.",
    multi_location_required:
      "Várias localizações requer o plano Business. Atualize para adicionar mais filiais.",
  },
  booking: {
    slot_expired: "Esse horário já passou. Escolha outro.",
    slot_unavailable: "Esse horário não está mais disponível.",
    slot_locked: "Esse horário está sendo reservado. Tente novamente.",
  },
  auth: {
    required: "Autenticação necessária.",
    customer_required: "Entre para concluir esta ação.",
    business_access_denied: "Você não tem acesso a este negócio.",
  },
  validation: "Verifique suas informações e tente novamente.",
  forbidden: "Você não tem permissão para realizar esta ação.",
  conflict: "Esta ação não pôde ser concluída devido a um conflito.",
  not_found: "{resource} não foi encontrado.",
  internal: {
    server_error: "Algo deu errado. Tente novamente mais tarde.",
  },
};
