# Sketch — the shape of the managed platform for my-frete (single region MVP).
# Not wired to a real backend/provider yet; fill in provider + module sources when the
# PoC graduates. Everything runs the same OCI image built by CI (Constitution §VIII).

terraform {
  required_version = ">= 1.7"
  # backend "s3" { ... }   # remote state — configure per environment
}

variable "environment" {
  type    = string
  default = "staging"
}

variable "api_image" {
  type        = string
  description = "GHCR image tag published by CI, e.g. ghcr.io/owner/my-frete/api:<sha>"
}

variable "route_provider_api_key" {
  type      = string
  sensitive = true
  default   = ""
}

# --- Network -----------------------------------------------------------------
# module "network" { source = "..."  cidr = "10.20.0.0/16"  azs = 2 }

# --- Data stores -----------------------------------------------------------
# module "postgres" {
#   source            = "..."            # managed PostgreSQL 16 with the postgis extension
#   engine_version    = "16"
#   extensions        = ["postgis"]
#   multi_az          = var.environment == "production"
#   deletion_protection = var.environment == "production"
# }
# module "redis" { source = "..."  engine_version = "7"  notify_keyspace_events = "Ex" }

# --- Secrets -------------------------------------------------------------------
# resource "secret" "jwt_signing_key"        { ... }         # 32+ chars, rotated
# resource "secret" "route_provider_api_key" { value = var.route_provider_api_key }

# --- API runtime ------------------------------------------------------------
# module "api" {
#   source        = "..."                  # container service / k8s deployment
#   image         = var.api_image
#   replicas      = var.environment == "production" ? 3 : 1
#   port          = 8080
#   health_path   = "/ready"
#   env = {
#     ConnectionStrings__Postgres = module.postgres.connection_string
#     ConnectionStrings__Redis    = module.redis.connection_string
#     Otlp__Endpoint              = module.observability.otlp_endpoint
#     RunMigrationsOnStartup      = "true"
#   }
#   secret_env = {
#     Jwt__SigningKey        = secret.jwt_signing_key.arn
#     ROUTE_PROVIDER_API_KEY = secret.route_provider_api_key.arn
#   }
#   rollout = { strategy = "canary", rollback_on = "slo_burn" }
# }

# --- Observability -----------------------------------------------------------
# module "observability" { source = "..."  dashboards = "./observability" }
