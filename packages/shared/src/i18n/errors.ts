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
    closed: "This business is not accepting online bookings right now.",
    staff_unavailable: "That team member is not available for the selected time.",
    staff_not_eligible: "That team member does not offer this service.",
    cart_empty: "Add at least one service to book.",
    addon_requires_parent: "Add-ons need a main service in your booking.",
    guest_limit: "Party size must be between 1 and {max}.",
    capacity_full: "This session is full for the selected party size.",
  },
  staff: {
    hours_invalid: "Staff hours are invalid.",
    leave_overlap: "That leave range overlaps an existing leave entry.",
    leave_not_found: "Leave entry was not found.",
  },
  payment: {
    invalid_amount: "Payment amount must be greater than zero.",
    invalid_currency: "Currency must be a 3-letter ISO code.",
    not_found: "Payment was not found.",
    provider_error: "Payment could not be processed. Please try again.",
    webhook_invalid: "Payment webhook verification failed.",
    already_processed: "This payment has already been processed.",
    refund_not_allowed: "This payment cannot be refunded.",
    deposit_not_configured: "Deposits are not configured for this business.",
  },
  auth: {
    required: "Authentication is required.",
    customer_required: "Sign in to complete this action.",
    business_access_denied: "You do not have access to this business.",
  },
  tenancy: {
    public_page: {
      invalid_template: "Choose a valid public page template.",
      invalid_accent: "Accent color must be a hex value like #0F766E.",
      sections_required: "Keep at least one of About, Services, or Visit visible.",
    },
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
    closed: "Cette entreprise n'accepte pas les réservations en ligne pour le moment.",
    staff_unavailable: "Ce membre de l'équipe n'est pas disponible pour l'heure choisie.",
    staff_not_eligible: "Ce membre de l'équipe n'offre pas ce service.",
    cart_empty: "Ajoutez au moins un service pour réserver.",
    addon_requires_parent: "Les options nécessitent un service principal.",
    guest_limit: "La taille du groupe doit être entre 1 et {max}.",
    capacity_full: "Cette séance est complète pour la taille de groupe choisie.",
  },
  staff: {
    hours_invalid: "Les horaires du membre de l'équipe sont invalides.",
    leave_overlap: "Cette période de congé chevauche une entrée existante.",
    leave_not_found: "Entrée de congé introuvable.",
  },
  payment: {
    invalid_amount: "Le montant du paiement doit être supérieur à zéro.",
    invalid_currency: "La devise doit être un code ISO à 3 lettres.",
    not_found: "Paiement introuvable.",
    provider_error: "Le paiement n'a pas pu être traité. Veuillez réessayer.",
    webhook_invalid: "La vérification du webhook de paiement a échoué.",
    already_processed: "Ce paiement a déjà été traité.",
    refund_not_allowed: "Ce paiement ne peut pas être remboursé.",
    deposit_not_configured: "Les acomptes ne sont pas configurés pour cette entreprise.",
  },
  auth: {
    required: "Authentification requise.",
    customer_required: "Connectez-vous pour effectuer cette action.",
    business_access_denied: "Vous n'avez pas accès à cette entreprise.",
  },
  tenancy: {
    public_page: {
      invalid_template: "Choisissez un modèle de page publique valide.",
      invalid_accent: "La couleur d'accent doit être un hexadécimal comme #0F766E.",
      sections_required: "Gardez au moins À propos, Services ou Visite visible.",
    },
  },
  validation: "Vérifiez vos informations et réessayez.",
  forbidden: "Vous n'avez pas l'autorisation d'effectuer cette action.",
  conflict: "Cette action n'a pas pu être effectuée en raison d'un conflit.",
  not_found: "{resource} est introuvable.",
  internal: {
    server_error: "Une erreur s'est produite. Veuillez réessayer plus tard.",
  },
};

