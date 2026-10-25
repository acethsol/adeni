import type { MessageTree } from "./types";

export const enErrorMessages: MessageTree = {
  intake: {"validation": "Check the intake form fields.", "conflict": "The form changed or was already submitted. Refresh before continuing.", "submission_closed": "This booking is no longer open for intake submission."},
  subscription: {
    booking_limit_reached:
      "This business reached its {limit} booking limit for this month. Upgrade to Pro for unlimited bookings.",
    multi_location_required:
      "Your plan does not include multiple locations.",
    messaging_required: "In-app messaging is not included on your current plan.",
  },
  queue: {
    closed: "Walk-ins are not accepting new guests right now.",
    full: "The waiting list is full. Try again later.",
    no_capacity:
      "Walk-ins would conflict with upcoming appointments. Try again later or book an appointment.",
  },
  messaging: {
    conversation_not_found: "Conversation was not found.",
    conversation_closed: "This conversation is closed.",
    message_body_invalid: "Message must be between 1 and 4000 characters, or include an attachment.",
    message_attachment_invalid: "That attachment cannot be sent.",
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
    invite_invalid: "This invite link is invalid or no longer available.",
    invite_expired: "This invite has expired. Ask the business owner to resend it.",
    invite_email_mismatch: "Sign in with the email address this invite was sent to.",
    last_owner: "Cannot remove or demote the last owner of this business.",
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
  permission: {
    denied: "You do not have permission to do that.",
  },
  tenancy: {
    public_page: {
      invalid_template: "Choose a valid public page template.",
      invalid_accent: "Accent color must be a hex value like #0F766E.",
      sections_required: "Keep at least one of About, Services, or Visit visible.",
    },
  },
  entitlements: {
    analytics_required: "Advanced analytics is not included on your current plan.",
    customer_segmentation: "Customer segmentation is not included on your current plan.",
    customer_history: "Full customer history is not included on your current plan.",
    location_limit: "Your plan allows up to {maxLocations} active location(s).",
    practitioner_limit: "Your plan allows up to {maxPractitioners} active team members.",
    waitlist: "Waitlist is not included on your current plan.",
    sms: "SMS notifications are not included on your current plan.",
    exports: "Exports are not included on your current plan.",
    excel_export: "Excel export is not included on your current plan.",
    multi_location_reporting:
      "Cross-location reporting requires the Business plan. Choose a location to export one branch, or upgrade for all locations.",
  },
  reporting: {
    report_not_found: "That report is not available.",
    format_not_supported: "That export format is not supported for this report.",
  },
  list: {
    sort_invalid: "That column cannot be used to sort this list.",
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
  intake: {"validation": "Vérifiez les champs du formulaire.", "conflict": "Le formulaire a changé ou a déjà été envoyé. Actualisez avant de continuer.", "submission_closed": "Cette réservation ne permet plus d’envoyer ce formulaire."},
  subscription: {
    booking_limit_reached:
      "Cette entreprise a atteint sa limite de {limit} réservations ce mois-ci. Passez à Pro pour des réservations illimitées.",
    multi_location_required:
      "Les emplacements multiples nécessitent le forfait Business. Passez à un forfait supérieur pour ajouter des succursales.",
    messaging_required: "La messagerie intégrée n'est pas incluse dans votre forfait actuel.",
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
    invite_invalid: "Ce lien d'invitation est invalide ou n'est plus disponible.",
    invite_expired: "Cette invitation a expiré. Demandez au propriétaire de la renvoyer.",
    invite_email_mismatch: "Connectez-vous avec l'adresse e-mail à laquelle l'invitation a été envoyée.",
    last_owner: "Impossible de retirer ou de rétrograder le dernier propriétaire.",
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
  permission: {
    denied: "Vous n'avez pas l'autorisation d'effectuer cette action.",
  },
  entitlements: {
    analytics_required: "Les analyses avancées ne sont pas incluses dans votre forfait actuel.",
    customer_segmentation: "La segmentation de la clientèle n'est pas incluse dans votre forfait actuel.",
    customer_history: "L'historique complet des clients n'est pas inclus dans votre forfait actuel.",
    location_limit: "Votre forfait permet jusqu'à {maxLocations} emplacement(s) actif(s).",
    practitioner_limit: "Votre forfait permet jusqu'à {maxPractitioners} membres d'équipe actifs.",
    waitlist: "La liste d'attente n'est pas incluse dans votre forfait actuel.",
    sms: "Les notifications par SMS ne sont pas incluses dans votre forfait actuel.",
    exports: "Les exportations ne sont pas incluses dans votre forfait actuel.",
    excel_export: "L'exportation Excel n'est pas incluse dans votre forfait actuel.",
    multi_location_reporting:
      "Les rapports multi-succursales nécessitent le forfait Business. Choisissez un emplacement pour une succursale, ou passez à un forfait supérieur pour tous les emplacements.",
  },
  reporting: {
    report_not_found: "Ce rapport n'est pas disponible.",
    format_not_supported: "Ce format d'exportation n'est pas pris en charge pour ce rapport.",
  },
  queue: {
    closed: "La file d'attente n'accepte pas de nouveaux clients pour le moment.",
    full: "La liste d'attente est pleine. Réessayez plus tard.",
    no_capacity:
      "Les clients sans rendez-vous entreraient en conflit avec les rendez-vous à venir. Réessayez plus tard ou prenez rendez-vous.",
  },
  messaging: {
    conversation_not_found: "Conversation introuvable.",
    conversation_closed: "Cette conversation est fermée.",
    message_body_invalid:
      "Le message doit contenir entre 1 et 4 000 caractères, ou inclure une pièce jointe.",
    message_attachment_invalid: "Cette pièce jointe ne peut pas être envoyée.",
  },
  list: {
    sort_invalid: "Cette colonne ne peut pas servir à trier cette liste.",
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

